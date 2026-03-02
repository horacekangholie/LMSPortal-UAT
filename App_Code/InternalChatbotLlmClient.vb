Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Net
Imports System.Web.Script.Serialization

' Handles HTTP communication with an internal/offline LLM endpoint.
Public Class InternalChatbotLlmClient
    Private ReadOnly _endpoint As String
    Private ReadOnly _model As String
    Private ReadOnly _timeoutMilliseconds As Integer

    Public Sub New(config As InternalChatbotConfig)
        _endpoint = config.LlmEndpoint
        _model = config.LlmModel
        _timeoutMilliseconds = config.LlmTimeoutSeconds * 1000
    End Sub

    ' Sends prompt to local Ollama runtime and returns plain text response.
    ' Supports both /api/generate and /api/chat endpoint shapes.
    Public Function Generate(prompt As String) As String
        If String.IsNullOrWhiteSpace(prompt) Then
            Throw New ApplicationException("Prompt cannot be empty.")
        End If

        Dim serializer As New JavaScriptSerializer()
        serializer.MaxJsonLength = Integer.MaxValue

        Dim body As String = serializer.Serialize(BuildRequestBody(prompt))
        Dim request As HttpWebRequest = CType(WebRequest.Create(_endpoint), HttpWebRequest)
        request.Method = "POST"
        request.ContentType = "application/json"
        request.Timeout = _timeoutMilliseconds
        request.ReadWriteTimeout = _timeoutMilliseconds

        Using requestStream As New StreamWriter(request.GetRequestStream())
            requestStream.Write(body)
        End Using

        Try
            Using response As HttpWebResponse = CType(request.GetResponse(), HttpWebResponse)
                Using reader As New StreamReader(response.GetResponseStream())
                    Dim responseBody As String = reader.ReadToEnd()
                    Return ParseResponse(responseBody, serializer)
                End Using
            End Using
        Catch ex As WebException
            Throw BuildWebException(ex)
        End Try
    End Function

    ' Builds request payload based on Ollama API style.
    Private Function BuildRequestBody(prompt As String) As Dictionary(Of String, Object)
        If IsChatEndpoint() Then
            Return New Dictionary(Of String, Object) From {
                {"model", _model},
                {"messages", New Object() {
                    New Dictionary(Of String, Object) From {
                        {"role", "user"},
                        {"content", prompt}
                    }
                }},
                {"stream", False}
            }
        End If

        Return New Dictionary(Of String, Object) From {
            {"model", _model},
            {"prompt", prompt},
            {"stream", False}
        }
    End Function

    ' Parses both /api/generate and /api/chat style response payloads.
    Private Function ParseResponse(responseBody As String, serializer As JavaScriptSerializer) As String
        Dim payload As Dictionary(Of String, Object) = serializer.Deserialize(Of Dictionary(Of String, Object))(responseBody)

        If payload Is Nothing Then
            Throw New ApplicationException("Local LLM returned an empty response payload.")
        End If

        If payload.ContainsKey("error") Then
            Throw New ApplicationException("Local LLM error: " & payload("error").ToString())
        End If

        If payload.ContainsKey("response") Then
            Return payload("response").ToString().Trim()
        End If

        If payload.ContainsKey("message") Then
            Dim messageObj As Object = payload("message")
            Dim messageDict As Dictionary(Of String, Object) = TryCast(messageObj, Dictionary(Of String, Object))

            If messageDict IsNot Nothing AndAlso messageDict.ContainsKey("content") Then
                Return messageDict("content").ToString().Trim()
            End If
        End If

        Throw New ApplicationException("Local LLM returned an unexpected response format.")
    End Function

    ' Detects whether configured endpoint is Ollama chat API.
    Private Function IsChatEndpoint() As Boolean
        Return _endpoint.IndexOf("/api/chat", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    ' Converts transport failures into actionable messages for internal users.
    Private Function BuildWebException(ex As WebException) As ApplicationException
        Try
            If ex.Response IsNot Nothing Then
                Using response As HttpWebResponse = CType(ex.Response, HttpWebResponse)
                    Dim responseBody As String = String.Empty
                    Dim responseStream As Stream = response.GetResponseStream()

                    If responseStream IsNot Nothing Then
                        Using reader As New StreamReader(responseStream)
                            responseBody = reader.ReadToEnd()
                        End Using
                    End If

                    Dim summary As String = String.Format("Local LLM endpoint returned HTTP {0} ({1}).", CInt(response.StatusCode), response.StatusDescription)

                    If response.StatusCode = HttpStatusCode.MethodNotAllowed Then
                        summary &= " Ollama APIs require POST; opening the endpoint URL in a browser (GET) will return 405."
                    End If

                    If Not String.IsNullOrWhiteSpace(responseBody) Then
                        summary &= " Body: " & responseBody
                    End If

                    Return New ApplicationException(summary)
                End Using
            End If
        Catch parseEx As Exception
            Return New ApplicationException("Failed to parse local LLM error response. " & parseEx.Message)
        End Try

        Dim message As String = "Unable to reach local LLM endpoint at " & _endpoint & "."
        If ex.Status = WebExceptionStatus.Timeout Then
            message &= " Request timed out."
        End If

        If ex.InnerException IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(ex.InnerException.Message) Then
            message &= " " & ex.InnerException.Message
        ElseIf Not String.IsNullOrWhiteSpace(ex.Message) Then
            message &= " " & ex.Message
        End If

        Return New ApplicationException(message)
    End Function


End Class
