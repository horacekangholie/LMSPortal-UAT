<%@ WebHandler Language="VB" Class="InternalChatbotHandler" %>

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Web
Imports System.Web.Script.Serialization

' HTTP endpoint for staff chatbot requests.
Public Class InternalChatbotHandler
    Implements IHttpHandler
    Implements System.Web.SessionState.IRequiresSessionState

    Private Shared ReadOnly Serializer As New JavaScriptSerializer()

    ' Handles request lifecycle: auth check, parse, dispatch, and response.
    Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
        context.Response.ContentType = "application/json"

        If context.Request.HttpMethod <> "POST" Then
            WriteError(context, 405, "Method not allowed")
            Return
        End If

        If context.Session("Login_Status") <> "Logged in" Then
            WriteError(context, 401, "Unauthorized")
            Return
        End If

        Try
            Dim input As InternalChatbotRequest = ParseRequest(context)
            input.RequestedBy = GetCurrentUser(context)

            Dim service As New InternalChatbotService()
            Dim response As InternalChatbotResponse = service.Ask(input)

            context.Response.StatusCode = 200
            context.Response.Write(Serializer.Serialize(response))
        Catch ex As ApplicationException
            WriteError(context, 400, ex.Message)
        Catch ex As Exception
            Dim detail As String = ex.Message
            If ex.InnerException IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(ex.InnerException.Message) Then
                detail &= " | " & ex.InnerException.Message
            End If

            WriteError(context, 500, "Unexpected server error: " & detail)
        End Try
    End Sub

    ' Parses JSON body and maps required fields into service DTO.
    Private Function ParseRequest(context As HttpContext) As InternalChatbotRequest
        Using reader As New StreamReader(context.Request.InputStream)
            Dim body As String = reader.ReadToEnd()
            Dim payload As Dictionary(Of String, Object) = Serializer.Deserialize(Of Dictionary(Of String, Object))(body)

            If payload Is Nothing OrElse Not payload.ContainsKey("question") Then
                Throw New ApplicationException("Question is required.")
            End If

            Return New InternalChatbotRequest With {
                .Question = payload("question").ToString().Trim()
            }
        End Using
    End Function

    ' Resolves current login identity used for auditing.
    Private Function GetCurrentUser(context As HttpContext) As String
        If context.Session("Login_Name") Is Nothing Then
            Return "Unknown"
        End If

        Return context.Session("Login_Name").ToString()
    End Function

    ' Writes standard JSON error payloads.
    Private Sub WriteError(context As HttpContext, statusCode As Integer, message As String)
        context.Response.StatusCode = statusCode
        context.Response.Write(Serializer.Serialize(New Dictionary(Of String, String) From {
            {"error", message}
        }))
    End Sub

    Public ReadOnly Property IsReusable() As Boolean Implements IHttpHandler.IsReusable
        Get
            Return False
        End Get
    End Property
End Class
