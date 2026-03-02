Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient

' Database access layer for schema introspection, query execution, and audit logging.
Public Class InternalChatbotRepository
    Private ReadOnly _connectionString As String
    Private ReadOnly _commandTimeoutSeconds As Integer

    Public Sub New(config As InternalChatbotConfig)
        _connectionString = config.ConnectionString
        _commandTimeoutSeconds = config.CommandTimeoutSeconds
    End Sub

    ' Produces compact schema text used in SQL-generation prompts.
    Public Function LoadSchemaSummary(maxColumns As Integer) As String
        Dim sql As String = "SELECT TOP (@MaxColumns) TABLE_NAME, COLUMN_NAME, DATA_TYPE " &
                            "FROM INFORMATION_SCHEMA.COLUMNS " &
                            "WHERE TABLE_SCHEMA = 'dbo' " &
                            "ORDER BY TABLE_NAME, ORDINAL_POSITION"

        Dim lines As New List(Of String)()
        Dim currentTable As String = String.Empty
        Dim currentColumns As New List(Of String)()

        Using con As New SqlConnection(_connectionString)
            Using cmd As New SqlCommand(sql, con)
                cmd.Parameters.AddWithValue("@MaxColumns", maxColumns)
                cmd.CommandTimeout = _commandTimeoutSeconds
                con.Open()

                Using dr As SqlDataReader = cmd.ExecuteReader()
                    While dr.Read()
                        Dim tableName As String = dr("TABLE_NAME").ToString()
                        Dim colDef As String = dr("COLUMN_NAME").ToString() & "(" & dr("DATA_TYPE").ToString() & ")"

                        If currentTable <> tableName Then
                            If Not String.IsNullOrWhiteSpace(currentTable) Then
                                lines.Add(currentTable & ": " & String.Join(", ", currentColumns.ToArray()))
                            End If
                            currentTable = tableName
                            currentColumns = New List(Of String)()
                        End If

                        currentColumns.Add(colDef)
                    End While
                End Using
            End Using
        End Using

        If Not String.IsNullOrWhiteSpace(currentTable) Then
            lines.Add(currentTable & ": " & String.Join(", ", currentColumns.ToArray()))
        End If

        Return String.Join(Environment.NewLine, lines.ToArray())
    End Function

    ' Executes validated SQL and converts result rows to serializable records.
    Public Function ExecuteQuery(sql As String) As InternalChatbotQueryOutput
        Dim dt As New DataTable()

        Using con As New SqlConnection(_connectionString)
            Using cmd As New SqlCommand(sql, con)
                cmd.CommandTimeout = _commandTimeoutSeconds
                con.Open()
                Using da As New SqlDataAdapter(cmd)
                    da.Fill(dt)
                End Using
            End Using
        End Using

        Dim records As List(Of Dictionary(Of String, Object)) = ConvertTableToRecords(dt)

        Return New InternalChatbotQueryOutput With {
            .DataTable = dt,
            .Records = records
        }
    End Function

    ' Persists request-level audit information for governance and traceability.
    Public Sub SaveAudit(entry As InternalChatbotAuditEntry)
        Dim insertSql As String = "INSERT INTO DB_Chatbot_Audit_Log (Requested_By, Question, Generated_SQL, Row_Count, Created_At) " &
                                  "VALUES (@RequestedBy, @Question, @GeneratedSql, @RowCount, GETDATE())"

        Using con As New SqlConnection(_connectionString)
            Using cmd As New SqlCommand(insertSql, con)
                cmd.CommandTimeout = _commandTimeoutSeconds
                cmd.Parameters.AddWithValue("@RequestedBy", entry.RequestedBy)
                cmd.Parameters.AddWithValue("@Question", entry.Question)
                cmd.Parameters.AddWithValue("@GeneratedSql", entry.GeneratedSql)
                cmd.Parameters.AddWithValue("@RowCount", entry.RowCount)
                con.Open()
                cmd.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    ' Converts DataTable rows to JSON-friendly dictionary records.
    Private Function ConvertTableToRecords(dt As DataTable) As List(Of Dictionary(Of String, Object))
        Dim records As New List(Of Dictionary(Of String, Object))()

        For Each row As DataRow In dt.Rows
            Dim record As New Dictionary(Of String, Object)()

            For Each col As DataColumn In dt.Columns
                Dim value As Object = row(col)
                If value Is DBNull.Value Then
                    record(col.ColumnName) = Nothing
                Else
                    record(col.ColumnName) = value
                End If
            Next

            records.Add(record)
        Next

        Return records
    End Function
End Class
