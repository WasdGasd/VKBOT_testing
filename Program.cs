using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VKBotRaw
{
    internal class Program
    {
        private static string token = "vk1.a.IRoEQiYy90vRfepWobiR7pdHs2goKowcQDjZk-MFMDuCKApfRAsAQN9Vj2FJKlZ-kskTwxPSlYtjEuaHQKyUDOm3ixes7S5OJbN2MSj4a7nCKZ6tsKGVGNNwPO2dmqcD-68TNFnmX3ifSRUGCDHFuu36rLUmxa76H9Fc38sbKtsR4LgU2X3dvHdDMa2n84FGT3lce50IkXof28tLmyzvZg";
        private static ulong groupId = 233846417;
        private static string apiVersion = "5.131";

        private static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("🚀 Запуск VK Bot...");

            using HttpClient client = new HttpClient();
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                Console.WriteLine("🔹 Получаю данные Long Poll сервера...");
                var serverResponse = await client.GetFromJsonAsync<LongPollServerResponse>(
                    $"https://api.vk.com/method/groups.getLongPollServer?group_id={groupId}&access_token={token}&v={apiVersion}"
                );

                if (serverResponse?.Response == null)
                {
                    Console.WriteLine("❌ Не удалось получить Long Poll сервер! Проверь токен и права.");
                    return;
                }

                string server = serverResponse.Response.Server;
                string key = serverResponse.Response.Key;
                string ts = serverResponse.Response.Ts;

                Console.WriteLine($"✅ Бот авторизован! Сервер: {server}");
                Console.WriteLine("⌛ Жду новых сообщений...");

                while (true)
                {
                    try
                    {
                        var pollResponse = await client.GetStringAsync(
                            $"{server}?act=a_check&key={key}&ts={ts}&wait=25"
                        );

                        var poll = JsonSerializer.Deserialize<LongPollUpdate>(pollResponse, jsonOptions);
                        if (poll == null)
                        {
                            continue;
                        }

                        ts = poll.Ts ?? ts;

                        if (poll.Updates == null || poll.Updates.Length == 0)
                        {
                            continue;
                        }

                        foreach (var update in poll.Updates)
                        {
                            if (update.Type == "message_new" && update.Object?.Message != null)
                            {
                                var msg = update.Object.Message.Text ?? "";
                                var userId = update.Object.Message.FromId;

                                Console.WriteLine($"💬 Новое сообщение от {userId}: {msg}");

                                string reply;
                                string? keyboard = null;

                                switch (msg.ToLower())
                                {
                                    case "/start":
                                    case "начать":
                                        reply = "Добро пожаловать! Выберите пункт 👇";
                                        keyboard = MainMenuKeyboard();
                                        break;

                                    case "информация":
                                        reply = "Выберите информацию 👇";
                                        keyboard = InfoMenuKeyboard();
                                        break;

                                    case "время работы":
                                        reply = "Аквапарк работает с 10:00 до 21:00 каждый день";
                                        break;

                                    case "контакты":
                                        reply = "Контакты\r\nПозвонить в Центр YES: (8172) 33-06-06\r\n\r\nНаписать е-mail: yes@yes35.ru \r\n\r\nЕдиная горячая линия по улучшению сервиса в ресторане: 8-800-200-67-71\r\n\r\nНайти YES ВКонтакте\nhttps://vk.com/yes35\r\n\r\nТолько самые интересные и актуальные новости, а также выгодные специальные предложения  в  Телеграм\nhttps://t.me/CentreYES35\r\n\r\nВсё лучшее детям! Предложения для групп в  WhatsApp\nhttps://chat.whatsapp.com/I4uygcAgoir7nyNoyYuMjL";
                                        break;

                                    case "назад":
                                        reply = "Главное меню:";
                                        keyboard = MainMenuKeyboard();
                                        break;

                                    case "билеты":
                                        reply = "Вот ссылка для покупки билета: https://aqua.yes35.ru/index.html";
                                        break;

                                    case "загруженность":
                                        reply = await GetParkLoadAsync(client);
                                        break;

                                    default:
                                        reply = "Я тебя не понял 😅 Напиши /help";
                                        break;
                                }

                                string url =
                                    $"https://api.vk.com/method/messages.send?user_id={userId}" +
                                    $"&random_id={Environment.TickCount}" +
                                    $"&message={Uri.EscapeDataString(reply)}" +
                                    $"&access_token={token}&v={apiVersion}";

                                if (keyboard != null)
                                    url += $"&keyboard={Uri.EscapeDataString(keyboard)}";

                                var sendResponse = await client.GetStringAsync(url);
                                Console.WriteLine($"✅ Ответ отправлен: {sendResponse}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Ошибка в цикле: {ex.Message}");
                        await Task.Delay(3000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка при инициализации: {ex.Message}");
            }
        }

        // Главная клавиатура
        private static string MainMenuKeyboard()
        {
            return JsonSerializer.Serialize(new
            {
                one_time = false,
                buttons = new[]
                {
                    new[]
                    {
                        new { action = new { type = "text", label = "Информация" }, color = "primary" },
                        new { action = new { type = "text", label = "Билеты" }, color = "positive" },
                        new { action = new { type = "text", label = "Загруженность" }, color = "secondary" }
                    }
                }
            });
        }

        // Подменю информации
        private static string InfoMenuKeyboard()
        {
            return JsonSerializer.Serialize(new
            {
                one_time = false,
                buttons = new[]
                {
                    new[]
                    {
                        new { action = new { type = "text", label = "Время работы" }, color = "primary" },
                        new { action = new { type = "text", label = "Контакты" }, color = "primary" },
                        new { action = new { type = "text", label = "Назад" }, color = "negative" }
                    }
                }
            });
        }

        // Получение загруженности через API
        private static async Task<string> GetParkLoadAsync(HttpClient client)
        {
            try
            {
                var requestData = new { SiteID = "1" };
                var response = await client.PostAsJsonAsync("https://apigateway.nordciti.ru/v1/aqua/CurrentLoad", requestData);

                if (!response.IsSuccessStatusCode)
                    return "Не удалось получить данные о загруженности 😔";

                var data = await response.Content.ReadFromJsonAsync<ParkLoadResponse>();

                if (data == null)
                    return "Не удалось обработать ответ сервера 😔";

                return $"Сейчас аквапарк загружен примерно на {data.Load}% ({data.Count} человек)";
            }
            catch
            {
                return "Не удалось получить данные о загруженности 😔";
            }
        }

        // Модель для ответа API
        public class ParkLoadResponse
        {
            public int Count { get; set; }
            public int Load { get; set; }
        }
    }

    // Модели VK Long Poll
    public class LongPollServerResponse { public LongPollServer Response { get; set; } = null!; }
    public class LongPollServer { public string Key { get; set; } = null!; public string Server { get; set; } = null!; public string Ts { get; set; } = null!; }
    public class LongPollUpdate { public string Ts { get; set; } = null!; public UpdateItem[] Updates { get; set; } = Array.Empty<UpdateItem>(); }
    public class UpdateItem { public string Type { get; set; } = null!; public UpdateObject? Object { get; set; } }
    public class UpdateObject { [JsonPropertyName("message")] public MessageItem? Message { get; set; } }
    public class MessageItem { public string Text { get; set; } = ""; [JsonPropertyName("from_id")] public long FromId { get; set; } }
}
