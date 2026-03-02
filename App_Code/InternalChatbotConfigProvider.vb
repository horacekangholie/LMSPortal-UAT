Imports System
Imports System.Configuration

' Loads chatbot runtime configuration from web.config with safe defaults.
Public Class InternalChatbotConfigProvider

    ' Builds a strongly-typed config object consumed by chatbot services.
    Public Shared Function Create() As InternalChatbotConfig
        Dim cfg As New InternalChatbotConfig()

        ' Reuse existing application SQL connection.
        cfg.ConnectionString = ConfigurationManager.ConnectionStrings("lmsConnectionString").ConnectionString

        ' Internal/offline LLM defaults (Ollama local endpoint + llama3.2:latest model).
        cfg.LlmEndpoint = GetAppSetting("InternalLLMEndpoint", "http://127.0.0.1:11434/api/generate")
        cfg.LlmModel = GetAppSetting("InternalLLMModel", "llama3.2:latest")
        cfg.LlmTimeoutSeconds = ParseInt(GetAppSetting("InternalLLMTimeoutSeconds", "120"), 120)

        ' Operational limits for safer query execution.
        cfg.MaxResultRows = ParseInt(GetAppSetting("InternalChatbotMaxResultRows", "100"), 100)
        cfg.CommandTimeoutSeconds = ParseInt(GetAppSetting("InternalChatbotCommandTimeoutSeconds", "60"), 60)

        Return cfg
    End Function

    ' Returns trimmed appSetting value or fallback when missing/blank.
    Private Shared Function GetAppSetting(key As String, fallback As String) As String
        Dim value As String = ConfigurationManager.AppSettings(key)
        If String.IsNullOrWhiteSpace(value) Then
            Return fallback
        End If

        Return value.Trim()
    End Function

    ' Parses positive integer values with fallback behavior.
    Private Shared Function ParseInt(raw As String, fallback As Integer) As Integer
        Dim parsed As Integer
        If Integer.TryParse(raw, parsed) AndAlso parsed > 0 Then
            Return parsed
        End If

        Return fallback
    End Function
End Class
