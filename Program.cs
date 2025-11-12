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
        // 🔑 Токен доступа сообщества
        private static string token = "vk1.a.IRoEQiYy90vRfepWobiR7pdHs2goKowcQDjZk-MFMDuCKApfRAsAQN9Vj2FJKlZ-kskTwxPSlYtjEuaHQKyUDOm3ixes7S5OJbN2MSj4a7nCKZ6tsKGVGNNwPO2dmqcD-68TNFnmX3ifSRUGCDHFuu36rLUmxa76H9Fc38sbKtsR4LgU2X3dvHdDMa2n84FGT3lce50IkXof28tLmyzvZg";

        // 🆔 ID сообщества
        private static ulong groupId = 233846417;

        // ⚙️ Версия API VK
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
                Console.WriteLine("⌛ Жду новых событий...");

                while (true)
                {
                    try
                    {
                        var pollResponse = await client.GetStringAsync($"{server}?act=a_check&key={key}&ts={ts}&wait=25");

                        var poll = JsonSerializer.Deserialize<LongPollUpdate>(pollResponse, jsonOptions);
                        if (poll == null) continue;

                        ts = poll.Ts ?? ts;
                        if (poll.Updates == null || poll.Updates.Length == 0) continue;

                        foreach (var update in poll.Updates)
                        {
                            // 🟢 Приветственное сообщение при первом заходе (message_allow)
                            if (update.Type == "message_allow" && update.Object?.UserId != null)
                            {
                                var userId = update.Object.UserId.Value;

                                Console.WriteLine($"👋 Новый пользователь разрешил сообщения: {userId}");

                                string welcomeText = "👋 Привет! Добро пожаловать в бота Центра YES!\n\n" +
                                                     "Я помогу вам:\n" +
                                                     "• Узнать время работы точек центра 🕒\n" +
                                                     "• Посмотреть загруженность аквапарка на данный момент 📊\n" +
                                                     "• Купить билеты в аквапарк онлайн 🎟\n\n" +
                                                     "Нажми кнопку ниже, чтобы начать! 🚀";

                                string keyboard = JsonSerializer.Serialize(new
                                {
                                    one_time = true,
                                    buttons = new[]
                                    {
                                        new[]
                                        {
                                            new { action = new { type = "text", label = "🚀 Начать" }, color = "positive" }
                                        }
                                    }
                                });

                                string url =
                                    $"https://api.vk.com/method/messages.send?user_id={userId}" +
                                    $"&random_id={Environment.TickCount}" +
                                    $"&message={Uri.EscapeDataString(welcomeText)}" +
                                    $"&keyboard={Uri.EscapeDataString(keyboard)}" +
                                    $"&access_token={token}&v={apiVersion}";

                                var sendResponse = await client.GetStringAsync(url);
                                Console.WriteLine($"✅ Приветственное сообщение отправлено: {sendResponse}");
                                continue;
                            }

                            // 💬 Обработка входящих сообщений
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
                                    case "🚀 начать":
                                        reply = "Добро пожаловать! Выберите пункт 👇";
                                        keyboard = MainMenuKeyboard();
                                        break;

                                    case "информация":
                                    case "ℹ️ информация":
                                        reply = "Выберите интересующую информацию 👇";
                                        keyboard = InfoMenuKeyboard();
                                        break;

                                    case "время работы":
                                    case "⏰ время работы":
                                        reply = "🏢 *Режим работы точек Центра YES:*\n\n" +

                                        "🌊 *Аквапарк*\n" +
                                        "⏰ 10:00 - 21:00 │ 📅 Ежедневно\n" +
                                        "💧 Бассейны, горки, сауны\n\n" +

                                        "🍽️ *Ресторан*\n" +
                                        "⏰ 10:00 - 21:00 │ 📅 Ежедневно\n" +
                                        "🍕 Кухня европейская и азиатская\n\n" +

                                        "🎮 *Игровой центр*\n" +
                                        "⏰ 10:00 - 18:00 │ 📅 Ежедневно\n" +
                                        "🎯 Автоматы и симуляторы\n\n" +

                                        "🦖 *Динопарк*\n" +
                                        "⏰ 10:00 - 18:00 │ 📅 Ежедневно\n" +
                                        "🦕 Интерактивные экспонаты\n\n" +

                                        "🏨 *Гостиница*\n" +
                                        "⏰ Круглосуточно │ 📅 Ежедневно\n" +
                                        "🛏️ Номера различных категорий\n\n" +

                                        "🔴 *Временно не работают:*\n" +
                                        "• 🧗‍ Веревочный парк\n" +
                                        "• 🧗‍ Скалодром\n" +
                                        "• 🎡 Парк аттракционов\n" +
                                        "• 🍔 MasterBurger\n\n" +

                                        "📞 *Уточнить информацию:* (8172) 33-06-06";
                                        break;

                                    case "контакты":
                                    case "📞 контакты":
                                        reply = "📞 *Контакты Центра YES*\n\n" +

                                                "📱 *Телефон для связи:*\n" +
                                                "•Основной: (8172) 33-06-06\n" +
                                                "•Ресторан: 8-800-200-67-71\n\n" +

                                                "📧 *Электронная почта:*\n" +
                                                " yes@yes35.ru\n\n" +

                                                "🌐 *Мы в соцсетях:*\n" +
                                                "ВКонтакте: vk.com/yes35\n" +
                                                "Telegram: t.me/CentreYES35\n" +
                                                "WhatsApp: ссылка в профиле\n\n" +

                                                "⏰ *Часы работы call-центра:*\n" +
                                                "🕙 09:00 - 22:00";
                                        break;



                                    case "назад":
                                    case "🔙 назад":
                                        reply = "Главное меню:";
                                        keyboard = MainMenuKeyboard();
                                        break;

                                    case "билеты":
                                    case "🎟 купить билеты":
                                        reply = "Выберите дату для сеанса:";
                                        keyboard = TicketsDateKeyboard();
                                        break;

                                    case "загруженность":
                                    case "📊 загруженность":
                                        reply = await GetParkLoadAsync(client);
                                        break;

                                    default:
                                        reply = "Я вас не понял, попробуйте еще раз 😅";
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

        // 🎛 Главное меню
        private static string MainMenuKeyboard()
        {
            return JsonSerializer.Serialize(new
            {
                one_time = false,
                buttons = new[]
                {
                    new[]
                    {
                        new { action = new { type = "text", label = "ℹ️ Информация" }, color = "primary" },
                        new { action = new { type = "text", label = "🎟 Купить билеты" }, color = "positive" },
                        new { action = new { type = "text", label = "📊 Загруженность" }, color = "secondary" }
                    },
                    new[]
                    {
                        new { action = new { type = "text", label = "🚀 Начать" }, color = "primary" }
                    }
                }
            });
        }

        // ℹ️ Меню информации
        private static string InfoMenuKeyboard()
        {
            return JsonSerializer.Serialize(new
            {
                one_time = false,
                buttons = new[]
                {
                    new[]
                    {
                        new { action = new { type = "text", label = "⏰ Время работы" }, color = "primary" },
                        new { action = new { type = "text", label = "📞 Контакты" }, color = "primary" },
                        new { action = new { type = "text", label = "🔙 Назад" }, color = "negative" }
                    }
                }
            });
        }

        // 🎟 Меню выбора даты билетов
        private static string TicketsDateKeyboard()
        {
            var buttons = new object[3][];
            var dateButtons = new object[3];

            for (int i = 0; i < 3; i++)
            {
                string dateStr = DateTime.Now.AddDays(i).ToString("dd.MM.yyyy");
                dateButtons[i] = new { action = new { type = "text", label = $"📅 {dateStr}" }, color = "primary" };
            }
            buttons[0] = dateButtons;

            var dateButtons2 = new object[2];
            for (int i = 3; i < 5; i++)
            {
                string dateStr = DateTime.Now.AddDays(i).ToString("dd.MM.yyyy");
                dateButtons2[i - 3] = new { action = new { type = "text", label = $"📅 {dateStr}" }, color = "primary" };
            }
            buttons[1] = dateButtons2;

            buttons[2] = new[]
            {
                new { action = new { type = "text", label = "🔙 Назад" }, color = "negative" }
            };

            return JsonSerializer.Serialize(new { one_time = true, buttons });
        }

        // 📊 Загруженность аквапарка
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

        public class ParkLoadResponse
        {
            public int Count { get; set; }
            public int Load { get; set; }
        }
    }

    // 🔹 Модели для VK API
    public class LongPollServerResponse { public LongPollServer Response { get; set; } = null!; }

    public class LongPollServer
    {
        public string Key { get; set; } = null!;
        public string Server { get; set; } = null!;
        public string Ts { get; set; } = null!;
    }

    public class LongPollUpdate
    {
        public string Ts { get; set; } = null!;
        public UpdateItem[] Updates { get; set; } = Array.Empty<UpdateItem>();
    }

    public class UpdateItem
    {
        public string Type { get; set; } = null!;
        public UpdateObject? Object { get; set; }
    }

    public class UpdateObject
    {
        [JsonPropertyName("message")]
        public MessageItem? Message { get; set; }

        [JsonPropertyName("user_id")]
        public long? UserId { get; set; } // Для message_allow
    }

    public class MessageItem
    {
        public string Text { get; set; } = "";
        [JsonPropertyName("from_id")] public long FromId { get; set; }
    }
}