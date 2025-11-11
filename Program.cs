using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace VKBotRaw
{
    internal class Program
    {
        private static string token = "vk1.a.04sSk9DZjbdgyzLMx4U2o-5m0wiVBRTk17OczNxiATr8-lCN1J7-7teRKJ8hLwRg5nW5VOUDCehKiA53x74kfWmZh0hqcB6wLPhbmPEBfHPMEuYWbBryc4KGWEjqo4ijGchRIIRdA1yGSywtYd5OUqEI9E8weu1xWEpJ294NYNn671vQ2XqwjPxVIBLK_4jgTXRZq2gp8gvk3UVL80Qu5w"; // Токен сообщества
        private static ulong groupId = 233846417;           // ID группы
        private static string apiVersion = "5.131";        // версия API VK

        private static async Task Main()
        {
            Console.WriteLine("Запуск VK Bot через HTTP...");

            using HttpClient client = new HttpClient();

            // Получаем сервер LongPoll
            var serverResponse = await client.GetFromJsonAsync<LongPollServerResponse>(
                $"https://api.vk.com/method/groups.getLongPollServer?group_id={groupId}&access_token={token}&v={apiVersion}"
            );

            if (serverResponse?.Response == null)
            {
                Console.WriteLine("Не удалось получить Long Poll сервер");
                return;
            }

            string server = serverResponse.Response.Server;
            string key = serverResponse.Response.Key;
            string ts = serverResponse.Response.Ts;

            Console.WriteLine("Бот авторизован! Жду сообщений...");

            while (true)
            {
                try
                {
                    var pollResponse = await client.GetStringAsync(
                        $"{server}?act=a_check&key={key}&ts={ts}&wait=25"
                    );

                    var poll = JsonSerializer.Deserialize<LongPollUpdate>(pollResponse);

                    if (poll?.Updates != null)
                    {
                        foreach (var update in poll.Updates)
                        {
                            // Новое сообщение
                            if (update.Type == "message_new")
                            {
                                var msg = update.Message.Text;
                                var userId = update.Message.FromId;
                                Console.WriteLine($"Сообщение от {userId}: {msg}");

                                string reply = msg.ToLower() switch
                                {
                                    "/start" or "привет" => "Привет! Я бот через HTTP API.",
                                    "/help" => "/start - начать\n/time - текущее время\n/help - помощь",
                                    "/time" => $"Сейчас {DateTime.Now:HH:mm:ss}",
                                    _ => "Я тебя не понял 😅 Напиши /help"
                                };

                                // Отправляем сообщение
                                await client.GetStringAsync(
                                    $"https://api.vk.com/method/messages.send?user_id={userId}&random_id={Environment.TickCount}&message={Uri.EscapeDataString(reply)}&access_token={token}&v={apiVersion}"
                                );
                            }
                        }
                        ts = poll.Ts; // обновляем ts
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                    await Task.Delay(3000);
                }
            }
        }
    }

    // Модели для десериализации
    public class LongPollServerResponse
    {
        public LongPollServer Response { get; set; } = null!;
    }

    public class LongPollServer
    {
        public string Key { get; set; } = null!;
        public string Server { get; set; } = null!;
        public string Ts { get; set; } = null!;
    }

    public class LongPollUpdate
    {
        public string Ts { get; set; } = null!;
        public UpdateItem[] Updates { get; set; } = null!;
    }

    public class UpdateItem
    {
        public string Type { get; set; } = null!;
        public MessageItem Message { get; set; } = null!;
    }

    public class MessageItem
    {
        public string Text { get; set; } = null!;
        public long FromId { get; set; }
    }
}
