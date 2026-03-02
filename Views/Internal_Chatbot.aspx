<%@ Page Title="LMSPortal - Internal Chatbot" Language="VB" MasterPageFile="~/MasterPages/Frontend.master" AutoEventWireup="false" %>

<asp:Content ID="Content1" ContentPlaceHolderID="head" Runat="Server">
    <link rel="stylesheet" href="/Content/ContentPage.css" />
    <style>
        /* Chatbot page layout and message styling */
        .chatbot-wrap { max-width: 980px; }
        .chat-window {
            border: 1px solid #ddd;
            border-radius: 8px;
            padding: 14px;
            min-height: 320px;
            max-height: 520px;
            overflow-y: auto;
            background: #fff;
        }
        .chat-message { margin-bottom: 14px; }
        .chat-user { font-weight: 600; color: #0d6efd; }
        .chat-bot { font-weight: 600; color: #198754; }
        .chat-meta { font-size: 12px; color: #6c757d; margin-top: 4px; white-space: pre-wrap; }
        .chat-loading { color: #6c757d; font-style: italic; }
    </style>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="PageContent" Runat="Server">
    <div class="chatbot-wrap">
        <h2>Internal Data Chatbot</h2>
        <p>This assistant can answer internal database questions only. All requests are read-only and audited.</p>

        <div id="chatWindow" class="chat-window" aria-live="polite"></div>

        <div class="input-group mt-3">
            <input id="questionInput" type="text" class="form-control" placeholder="Ask a data question..." />
            <button id="sendBtn" class="btn btn-primary" type="button">Ask</button>
        </div>
    </div>

    <script>
        (function () {
            const chatWindow = document.getElementById('chatWindow');
            const questionInput = document.getElementById('questionInput');
            const sendBtn = document.getElementById('sendBtn');

            // Renders one chat message block with optional metadata.
            function addMessage(roleClass, roleLabel, text, meta) {
                const container = document.createElement('div');
                container.className = 'chat-message';

                const label = document.createElement('div');
                label.className = roleClass;
                label.textContent = roleLabel;

                const content = document.createElement('div');
                content.textContent = text;

                container.appendChild(label);
                container.appendChild(content);

                if (meta) {
                    const metaNode = document.createElement('div');
                    metaNode.className = 'chat-meta';
                    metaNode.textContent = meta;
                    container.appendChild(metaNode);
                }

                chatWindow.appendChild(container);
                chatWindow.scrollTop = chatWindow.scrollHeight;

                return container;
            }

            // Shows temporary bot status while request is in progress.
            function addLoading() {
                return addMessage('chat-loading', 'Assistant', 'Thinking...');
            }

            // Sends one user question to the internal chatbot handler.
            async function sendQuestion() {
                const question = questionInput.value.trim();
                if (!question) {
                    return;
                }

                addMessage('chat-user', 'You', question);
                questionInput.value = '';
                sendBtn.disabled = true;
                const loadingNode = addLoading();

                try {
                    const response = await fetch('/Services/InternalChatbot.ashx', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ question: question })
                    });

                    const payload = await response.json();
                    chatWindow.removeChild(loadingNode);

                    if (!response.ok) {
                        addMessage('chat-bot', 'Assistant', payload.error || 'Request failed.');
                        return;
                    }

                    const meta = 'SQL: ' + payload.sql + '\nRows: ' + payload.rowCount;
                    addMessage('chat-bot', 'Assistant', payload.answer || '(No answer returned)', meta);
                } catch (e) {
                    if (loadingNode.parentNode === chatWindow) {
                        chatWindow.removeChild(loadingNode);
                    }
                    addMessage('chat-bot', 'Assistant', 'Unable to connect to internal chatbot service.');
                } finally {
                    sendBtn.disabled = false;
                    questionInput.focus();
                }
            }

            // Bind user actions.
            sendBtn.addEventListener('click', sendQuestion);
            questionInput.addEventListener('keypress', function (event) {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    sendQuestion();
                }
            });
        })();
    </script>
</asp:Content>
