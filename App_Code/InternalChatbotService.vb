Imports System
Imports System.Collections.Generic
Imports System.Data.SqlClient
Imports System.Web.Script.Serialization

' Orchestrates end-to-end chatbot flow from question to audited answer.
Public Class InternalChatbotService
    Private ReadOnly _config As InternalChatbotConfig
    Private ReadOnly _repository As InternalChatbotRepository
    Private ReadOnly _llmClient As InternalChatbotLlmClient

    Public Sub New()
        _config = InternalChatbotConfigProvider.Create()
        _repository = New InternalChatbotRepository(_config)
        _llmClient = New InternalChatbotLlmClient(_config)
    End Sub

    ' Main use-case: generate SQL -> guard -> execute -> summarize -> audit.
    Public Function Ask(input As InternalChatbotRequest) As InternalChatbotResponse
        ValidateInput(input)

        Dim schemaSummary As String = _repository.LoadSchemaSummary(60)
        Dim sqlPrompt As String = BuildSqlGenerationPrompt(input.Question, schemaSummary)

        Dim rawSql As String = _llmClient.Generate(sqlPrompt)
        Dim safeSql As String = InternalChatbotSqlGuard.EnsureSafeSelectOnly(rawSql, _config.MaxResultRows)

        ' Execute the generated SQL and automatically attempt one repair pass when SQL syntax fails.
        Dim executedSql As String = safeSql
        Dim queryResult As InternalChatbotQueryOutput = ExecuteWithOneRepairAttempt(input.Question, schemaSummary, executedSql)

        Dim answerPrompt As String = BuildAnswerPrompt(input.Question, executedSql, queryResult.Records)
        Dim finalAnswer As String = _llmClient.Generate(answerPrompt)

        _repository.SaveAudit(New InternalChatbotAuditEntry With {
            .RequestedBy = input.RequestedBy,
            .Question = input.Question,
            .GeneratedSql = executedSql,
            .RowCount = queryResult.Records.Count
        })

        Return New InternalChatbotResponse With {
            .Answer = finalAnswer,
            .Sql = executedSql,
            .RowCount = queryResult.Records.Count,
            .Data = queryResult.Records
        }
    End Function

    ' Executes SQL and retries once by asking the LLM to fix syntax while keeping read-only constraints.
    Private Function ExecuteWithOneRepairAttempt(question As String, schemaSummary As String, ByRef safeSql As String) As InternalChatbotQueryOutput
        Try
            Return _repository.ExecuteQuery(safeSql)
        Catch ex As SqlException
            Dim repairPrompt As String = BuildSqlRepairPrompt(question, schemaSummary, safeSql, ex.Message)
            Dim repairedRawSql As String = _llmClient.Generate(repairPrompt)
            safeSql = InternalChatbotSqlGuard.EnsureSafeSelectOnly(repairedRawSql, _config.MaxResultRows)
            Return _repository.ExecuteQuery(safeSql)
        End Try
    End Function

    ' Validates service input and ensures audit identity is always populated.
    Private Sub ValidateInput(input As InternalChatbotRequest)
        If input Is Nothing Then
            Throw New ApplicationException("Invalid chatbot request.")
        End If

        If String.IsNullOrWhiteSpace(input.Question) Then
            Throw New ApplicationException("Question is required.")
        End If

        If String.IsNullOrWhiteSpace(input.RequestedBy) Then
            input.RequestedBy = "Unknown"
        End If
    End Sub

    ' Prompt template used for SQL synthesis from user question.
    Private Function BuildSqlGenerationPrompt(question As String, schemaSummary As String) As String
        Return "You are an assistant that writes read-only SQL Server queries for internal staff." & vbLf &
               "Return SQL only with no markdown and no explanation." & vbLf &
               "Rules:" & vbLf &
               "- One SELECT statement only." & vbLf &
               "- No DML/DDL (INSERT, UPDATE, DELETE, DROP, ALTER, EXEC...)." & vbLf &
               "- Wrap identifiers (table names, column names, aliases) in square brackets [] to avoid keyword conflicts (e.g. [Group])." & vbLf &
               "- Use a practical and concise projection." & vbLf &
               "- Prefer deterministic ordering for recent records." & vbLf & vbLf &
               "Schema summary:" & vbLf & schemaSummary & vbLf & vbLf &
               "Question: " & question
    End Function

    ' Prompt template used when first SQL execution fails and a corrected query is needed.
    Private Function BuildSqlRepairPrompt(question As String, schemaSummary As String, failedSql As String, errorMessage As String) As String
        Return "The previous SQL Server query failed. Produce a corrected query." & vbLf &
               "Return SQL only with no markdown and no explanation." & vbLf &
               "Rules:" & vbLf &
               "- One SELECT statement only." & vbLf &
               "- No DML/DDL/admin statements." & vbLf &
               "- Keep semantics aligned to the original user question." & vbLf &
               "- Wrap identifiers (table names, column names, aliases) in square brackets [] to avoid keyword conflicts." & vbLf & vbLf &
               "Question: " & question & vbLf &
               "Schema summary:" & vbLf & schemaSummary & vbLf & vbLf &
               "Failed SQL:" & vbLf & failedSql & vbLf & vbLf &
               "SQL Server error:" & vbLf & errorMessage
    End Function

    ' Prompt template used for natural-language answer generation.
    Private Function BuildAnswerPrompt(question As String, sql As String, rows As List(Of Dictionary(Of String, Object))) As String
        Dim serializer As New JavaScriptSerializer()
        serializer.MaxJsonLength = Integer.MaxValue

        Dim json As String = serializer.Serialize(rows)

        Return "You are an internal assistant answering from SQL result data only." & vbLf &
               "If there are no rows, state clearly that no data was found." & vbLf &
               "Do not invent values and keep the answer concise." & vbLf & vbLf &
               "Question: " & question & vbLf &
               "SQL: " & sql & vbLf &
               "Rows JSON: " & json
    End Function
End Class
