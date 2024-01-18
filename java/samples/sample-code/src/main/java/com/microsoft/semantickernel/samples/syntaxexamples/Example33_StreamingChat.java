package com.microsoft.semantickernel.samples.syntaxexamples;

import com.azure.ai.openai.OpenAIAsyncClient;
import com.azure.ai.openai.OpenAIClientBuilder;
import com.azure.core.credential.AzureKeyCredential;
import com.azure.core.credential.KeyCredential;
import com.microsoft.semantickernel.aiservices.openai.chatcompletion.OpenAIChatCompletion;
import com.microsoft.semantickernel.chatcompletion.AuthorRole;
import com.microsoft.semantickernel.chatcompletion.ChatCompletionService;
import com.microsoft.semantickernel.chatcompletion.ChatHistory;
import com.microsoft.semantickernel.chatcompletion.ChatMessageContent;
import com.microsoft.semantickernel.chatcompletion.StreamingChatMessageContent;

public class Example33_StreamingChat {

    private static final String CLIENT_KEY = System.getenv("CLIENT_KEY");
    private static final String AZURE_CLIENT_KEY = System.getenv("AZURE_CLIENT_KEY");

    // Only required if AZURE_CLIENT_KEY is set
    private static final String CLIENT_ENDPOINT = System.getenv("CLIENT_ENDPOINT");
    private static final String MODEL_ID = System.getenv()
        .getOrDefault("MODEL_ID", "gpt-35-turbo");


    public static void main(String[] args) {
        System.out.println("======== Open AI - ChatGPT Streaming ========");

        OpenAIAsyncClient client;

        if (AZURE_CLIENT_KEY != null) {
            client = new OpenAIClientBuilder()
                .credential(new AzureKeyCredential(AZURE_CLIENT_KEY))
                .endpoint(CLIENT_ENDPOINT)
                .buildAsyncClient();

        } else {
            client = new OpenAIClientBuilder()
                .credential(new KeyCredential(CLIENT_KEY))
                .buildAsyncClient();
        }

        ChatCompletionService chatGPT = OpenAIChatCompletion.builder()
                .withModelId(MODEL_ID)
                .withOpenAIAsyncClient(client)
                .build();

        System.out.println("Chat content:");
        System.out.println("------------------------");

        ChatHistory chatHistory = new ChatHistory("You are a librarian, expert about books");

        // First user message
        chatHistory.addUserMessage("Hi, I'm looking for book suggestions");
        messageOutput(chatHistory);

        GPTReply(chatGPT, chatHistory);

        chatHistory.addUserMessage("I love history and philosophy, I'd like to learn something new about Greece, any suggestion");
        messageOutput(chatHistory);

        GPTReply(chatGPT, chatHistory);
    }

    private static void messageOutput(ChatHistory chatHistory) {
        var message = chatHistory.getLastMessage().get();
        System.out.println(message.getAuthorRole() + ": " + message.getContent());
        System.out.println("------------------------");
    }

    private static void GPTReply(ChatCompletionService chatGPT, ChatHistory chatHistory) {
        var reply = chatGPT.getStreamingChatMessageContentsAsync(chatHistory, null, null);
        System.out.print(AuthorRole.ASSISTANT + ": ");

        String message = reply
                .doOnNext(streamingChatMessage -> {
                    String content = streamingChatMessage.getContent();
                    System.out.print(content);
                })
                .map(StreamingChatMessageContent::getContent)
                .collect(StringBuilder::new, StringBuilder::append)
                .map(StringBuilder::toString)
                .block();

        System.out.println("\n------------------------");
        chatHistory.addAssistantMessage(message);
    }
}
