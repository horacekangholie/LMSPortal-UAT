Imports System
Imports System.Collections.Generic
Imports System.Data

' Root chatbot runtime configuration used across service components.
Public Class InternalChatbotConfig
    Public Property ConnectionString As String
    Public Property LlmEndpoint As String
    Public Property LlmModel As String
    Public Property LlmTimeoutSeconds As Integer
    Public Property MaxResultRows As Integer
    Public Property CommandTimeoutSeconds As Integer
End Class

' Request DTO passed from handler to service layer.
Public Class InternalChatbotRequest
    Public Property Question As String
    Public Property RequestedBy As String
End Class

' Response DTO returned to API caller/UI.
Public Class InternalChatbotResponse
    Public Property Answer As String
    Public Property Sql As String
    Public Property RowCount As Integer
    Public Property Data As List(Of Dictionary(Of String, Object))
End Class

' Audit DTO persisted to chatbot audit table.
Public Class InternalChatbotAuditEntry
    Public Property RequestedBy As String
    Public Property Question As String
    Public Property GeneratedSql As String
    Public Property RowCount As Integer
End Class

' Repository output that contains raw table + serialized records.
Public Class InternalChatbotQueryOutput
    Public Property DataTable As DataTable
    Public Property Records As List(Of Dictionary(Of String, Object))
End Class
