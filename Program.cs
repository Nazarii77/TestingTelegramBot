using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var botToken = configuration["BOT_TOKEN"]
    ?? configuration["BotSettings:Token"]
    ?? configuration["BotSettings__Token"];

var adminChatId = configuration["ADMIN_CHAT_ID"]
    ?? configuration["BotSettings:AdminChatId"]
    ?? configuration["BotSettings__AdminChatId"];

if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(adminChatId))
{
    Console.WriteLine("Please set BOT_TOKEN/ADMIN_CHAT_ID or BotSettings:Token/BotSettings:AdminChatId.");
    return;
}

var botClient = new TelegramBotClient(botToken);
var me = await botClient.GetMe();
Console.WriteLine($"Bot started: {me.Username}");

using var cts = new CancellationTokenSource();

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    errorHandler: HandlePollingErrorAsync,
    receiverOptions: new ReceiverOptions { AllowedUpdates = [] },
    cancellationToken: cts.Token);

Console.WriteLine("Bot is running. Press Enter to stop.");
Console.ReadLine();
cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.CallbackQuery is { } callbackQuery)
    {
        if (callbackQuery.Data == "restart")
        {
            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);
            await botClient.SendMessage(
                chatId: callbackQuery.From.Id,
                text: "Привіт! Як тебе звати?",
                cancellationToken: cancellationToken);
        }

        return;
    }

    if (update.Message is not { } message)
    {
        return;
    }

    if (message.Text is not { } text)
    {
        return;
    }

    if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
    {
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "Привіт! Як тебе звати?",
            cancellationToken: cancellationToken);
        return;
    }

    if (message.Chat.Id.ToString() == adminChatId)
    {
        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: $"Отримано ім'я: {text}",
            cancellationToken: cancellationToken);
        return;
    }

    var profileButton = InlineKeyboardButton.WithUrl(
        "Відкрити профіль",
        $"tg://user?id={message.From?.Id ?? 0}");

    var profileKeyboard = new InlineKeyboardMarkup(new[] { new[] { profileButton } });

    await botClient.SendMessage(
        chatId: long.Parse(adminChatId),
        text: $"Новий користувач: {text}",
        replyMarkup: profileKeyboard,
        cancellationToken: cancellationToken);

    var restartButton = InlineKeyboardButton.WithCallbackData("Restart", "restart");
    var restartKeyboard = new InlineKeyboardMarkup(new[] { new[] { restartButton } });

    await botClient.SendMessage(
        chatId: message.Chat.Id,
        text: $"Дякую, {text}! Я передав твоє ім'я адміну.",
        replyMarkup: restartKeyboard,
        cancellationToken: cancellationToken);
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    Console.WriteLine($"Polling error: {exception.Message}");
    return Task.CompletedTask;
}
