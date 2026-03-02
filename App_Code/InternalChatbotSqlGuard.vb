Imports System
Imports System.Text.RegularExpressions

' Defensive SQL validator to enforce read-only, single-statement usage.
Public Class InternalChatbotSqlGuard
    Private Shared ReadOnly ForbiddenKeywordPattern As String = "\b(insert|update|delete|drop|alter|truncate|create|exec|execute|merge|grant|revoke|backup|restore|sp_|xp_)\b"

    ' Validates generated SQL and auto-enforces TOP N if absent.
    Public Shared Function EnsureSafeSelectOnly(rawSql As String, maxRows As Integer) As String
        Dim sql As String = NormalizeSql(rawSql)

        If String.IsNullOrWhiteSpace(sql) Then
            Throw New ApplicationException("Model did not return SQL.")
        End If

        Dim lowered As String = sql.ToLowerInvariant()

        ' Only allow SELECT as statement root.
        If Not lowered.StartsWith("select") Then
            Throw New ApplicationException("Only SELECT queries are allowed.")
        End If

        ' Block known DML/DDL/admin patterns.
        If Regex.IsMatch(lowered, ForbiddenKeywordPattern) Then
            Throw New ApplicationException("Unsafe SQL detected.")
        End If

        ' Reject stacked statements.
        If lowered.Contains(";") Then
            Throw New ApplicationException("Multiple SQL statements are not allowed.")
        End If

        Return EnsureTopLimit(sql, maxRows)
    End Function

    ' Removes markdown wrappers if model responds with fenced SQL.
    Private Shared Function NormalizeSql(rawSql As String) As String
        If rawSql Is Nothing Then
            Return String.Empty
        End If

        Dim cleaned As String = Regex.Replace(rawSql, "```sql", "", RegexOptions.IgnoreCase)
        cleaned = cleaned.Replace("```", "")
        Return cleaned.Trim()
    End Function

    ' Adds SELECT TOP limit to reduce accidental oversized reads.
    Private Shared Function EnsureTopLimit(sql As String, maxRows As Integer) As String
        Dim hasTop As Boolean = Regex.IsMatch(sql, "^\s*select\s+top\s*\(", RegexOptions.IgnoreCase) OrElse
                          Regex.IsMatch(sql, "^\s*select\s+top\s+\d+", RegexOptions.IgnoreCase)

        If hasTop Then
            Return sql
        End If

        Return Regex.Replace(sql,
                             "^\s*select\s+",
                             "SELECT TOP " & maxRows.ToString() & " ",
                             RegexOptions.IgnoreCase)
    End Function
End Class
