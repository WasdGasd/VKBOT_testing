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
        //Токен доступа сообщества
        private static string token = "vk1.a.IRoEQiYy90vRfepWobiR7pdHs2goKowcQDjZk-MFMDuCKApfRAsAQN9Vj2FJKlZ-kskTwxPSlYtjEuaHQKyUDOm3ixes7S5OJbN2MSj4a7nCKZ6tsKGVGNNwPO2dmqcD-68TNFnmX3ifSRUGCDHFuu36rLUmxa76H9Fc38sbKtsR4LgU2X3dvHdDMa2n84FGT3lce50IkXof28tLmyzvZg";

        //ID сообщества ВКонтакте
        private static ulong groupId = 233846417;

        //Версия API VK
        private static string apiVersion = "5.131";

        private static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("🚀 Запуск VK Bot...");

            // HTTP клиент для работы с VK API
            using HttpClient client = new HttpClient();

            // Настройка JSON — чтобы не зависеть от регистра
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            try
            {
                // Получаем параметры Long Poll сервера (адрес, ключ)
                Console.WriteLine("🔹 Получаю данные Long Poll сервера...");
                var serverResponse = await client.GetFromJsonAsync<LongPollServerResponse>(
                    $"https://api.vk.com/method/groups.getLongPollServer?group_id={groupId}&access_token={token}&v={apiVersion}"
                );

                // Проверяем, что сервер получен
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

                //Основной цикл прослушивания событий
                while (true)
                {
                    try
                    {
                        // Отправляем запрос к Long Poll серверу
                        var pollResponse = await client.GetStringAsync(
                            $"{server}?act=a_check&key={key}&ts={ts}&wait=25"
                        );

                        // Десериализуем ответ
                        var poll = JsonSerializer.Deserialize<LongPollUpdate>(pollResponse, jsonOptions);
                        if (poll == null) continue;

                        // Обновляем ts (текущий "временной маркер" событий)
                        ts = poll.Ts ?? ts;

                        // Если нет новых событий — продолжаем ждать
                        if (poll.Updates == null || poll.Updates.Length == 0) continue;

                        // Обрабатываем каждое новое событие
                        foreach (var update in poll.Updates)
                        {
                            // Проверяем, что это новое сообщение
                            if (update.Type == "message_new" && update.Object?.Message != null)
                            {
                                var msg = update.Object.Message.Text ?? "";
                                var userId = update.Object.Message.FromId;

                                Console.WriteLine($"💬 Новое сообщение от {userId}: {msg}");

                                string reply; // текст ответа
                                string? keyboard = null; // клавиатура (если нужна)

                                //Основная логика бота — ответы на команды
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
                                        reply = "Аквапарк работает с 10:00 до 21:00 каждый день";
                                        break;

                                    case "контакты":
                                    case "📞 контакты":
                                        reply = "Контакты\r\nПозвонить в Центр YES: (8172) 33-06-06\r\n\r\nНаписать e-mail: yes@yes35.ru\r\n\r\nГорячая линия ресторана: 8-800-200-67-71\nВКонтакте: https://vk.com/yes35\nTelegram: https://t.me/CentreYES35\nWhatsApp: https://chat.whatsapp.com/I4uygcAgoir7nyNoyYuMjL";
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

                                // Формируем URL для отправки ответа пользователю
                                string url =
                                    $"https://api.vk.com/method/messages.send?user_id={userId}" +
                                    $"&random_id={Environment.TickCount}" +
                                    $"&message={Uri.EscapeDataString(reply)}" +
                                    $"&access_token={token}&v={apiVersion}";

                                // Если есть клавиатура — добавляем её
                                if (keyboard != null)
                                    url += $"&keyboard={Uri.EscapeDataString(keyboard)}";

                                // Отправляем сообщение пользователю
                                var sendResponse = await client.GetStringAsync(url);
                                Console.WriteLine($"✅ Ответ отправлен: {sendResponse}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Если ошибка при работе с сервером — подождём и продолжим
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

        // КЛАВИАТУРЫ 

        // Главное меню — основные функции
        private static string MainMenuKeyboard()
        {
            return JsonSerializer.Serialize(new
            {
                one_time = false, // клавиатура остаётся после нажатия
                buttons = new[]
                {
                    // Первый ряд кнопок
                    new[]
                    {
                        new { action = new { type = "text", label = "ℹ️ Информация" }, color = "primary" },
                        new { action = new { type = "text", label = "🎟 Купить билеты" }, color = "positive" },
                        new { action = new { type = "text", label = "📊 Загруженность" }, color = "secondary" }
                    },
                    // Второй ряд
                    new[]
                    {
                        new { action = new { type = "text", label = "🚀 Начать" }, color = "primary" }
                    }
                }
            });
        }

        // Меню информации
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

        // Меню выбора даты покупки билетов
        private static string TicketsDateKeyboard()
        {
            var buttons = new object[3][];
            var dateButtons = new object[3];

            // Первый ряд (3 ближайшие даты)
            for (int i = 0; i < 3; i++)
            {
                string dateStr = DateTime.Now.AddDays(i).ToString("dd.MM.yyyy");
                dateButtons[i] = new { action = new { type = "text", label = $"📅 {dateStr}" }, color = "primary" };
            }
            buttons[0] = dateButtons;

            // Второй ряд (ещё 2 даты)
            var dateButtons2 = new object[2];
            for (int i = 3; i < 5; i++)
            {
                string dateStr = DateTime.Now.AddDays(i).ToString("dd.MM.yyyy");
                dateButtons2[i - 3] = new { action = new { type = "text", label = $"📅 {dateStr}" }, color = "primary" };
            }
            buttons[1] = dateButtons2;

            // Последний ряд — кнопка "Назад"
            buttons[2] = new[]
            {
                new { action = new { type = "text", label = "🔙 Назад" }, color = "negative" }
            };

            return JsonSerializer.Serialize(new { one_time = true, buttons });
        }

        //ЗАГРУЖЕННОСТЬ АКВАПАРКА

        // Метод получения данных с внешнего API (текущая загруженность аквапарка)
        private static async Task<string> GetParkLoadAsync(HttpClient client)
        {
            try
            {
                var requestData = new { SiteID = "1" }; // JSON запрос
                var response = await client.PostAsJsonAsync("https://apigateway.nordciti.ru/v1/aqua/CurrentLoad", requestData);

                // Проверка успешного ответа
                if (!response.IsSuccessStatusCode)
                    return "Не удалось получить данные о загруженности 😔";

                // Парсим JSON в объект
                var data = await response.Content.ReadFromJsonAsync<ParkLoadResponse>();

                if (data == null)
                    return "Не удалось обработать ответ сервера 😔";

                // Возвращаем текстовый ответ
                return $"Сейчас аквапарк загружен примерно на {data.Load}% ({data.Count} человек)";
            }
            catch
            {
                return "Не удалось получить данные о загруженности 😔";
            }
        }

        // Модель данных загруженности
        public class ParkLoadResponse
        {
            public int Count { get; set; } // количество людей
            public int Load { get; set; }  // процент загруженности
        }
    }

    // МОДЕЛИ ДАННЫХ ДЛЯ VK API

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
    }

    public class MessageItem
    {
        public string Text { get; set; } = "";
        [JsonPropertyName("from_id")] public long FromId { get; set; }
    }
}
