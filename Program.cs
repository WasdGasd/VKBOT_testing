using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;

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

        // Словарь для хранения выбранной пользователем даты и сеанса
        private static readonly ConcurrentDictionary<long, (string date, string session)> userSelectedData = new();

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
                            // 🟢 Новый пользователь разрешил сообщения
                            if (update.Type == "message_allow" && update.Object?.UserId != null)
                            {
                                var userId = update.Object.UserId.Value;
                                Console.WriteLine($"👋 Новый пользователь разрешил сообщения: {userId}");

                                string welcomeText = "🌊 ДОБРО ПОЛОЖАЛОВАТЬ В ЦЕНТР YES!\n\n" +
"Я ваш персональный помощник для организации незабываемого отдыха! 🎯\n\n" +

"🎟 УМНАЯ ПОКУПКА БИЛЕТОВ\n" +
"- Выбор идеальной даты посещения\n" +
"- Подбор сеанса с учетом загруженности\n" +
"- Раздельный просмотр тарифов: взрослые/детские\n" +
"- Прозрачные цены без скрытых комиссий\n" +
"- Мгновенный переход к безопасной оплате онлайн\n\n" +

"📊 ОНЛАЙН-МОНИТОРИНГ ЗАГРУЖЕННОСТИ\n" +
"- Реальная картина посещаемости в реальном времени\n" +
"- Точное количество гостей в аквапарке\n" +
"- Процент заполненности для комфортного планирования\n" +
"- Рекомендации по лучшему времени для визита\n\n" +

"ℹ️ ПОЛНАЯ ИНФОРМАЦИЯ О ЦЕНТРЕ\n" +
"- Актуальное расписание всех зон и аттракционов\n" +
"- Контакты и способы связи с администрацией\n" +
"- Информация о временно закрытых объектах\n" +
"- Все необходимое для комфортного планирования\n\n" +

"🚀 Начните прямо сейчас!\n" +
"Выберите раздел в меню ниже, и я помогу организовать ваш идеальный визит! ✨\n\n" +
"💫 Центр YES - где рождаются воспоминания!";

                                string keyboard = JsonSerializer.Serialize(new
                                {
                                    one_time = true,
                                    buttons = new[] { new[] { new { action = new { type = "text", label = "🚀 Начать" }, color = "positive" } } }
                                });

                                string url =
                                    $"https://api.vk.com/method/messages.send?user_id={userId}" +
                                    $"&random_id={Environment.TickCount}" +
                                    $"&message={Uri.EscapeDataString(welcomeText)}" +
                                    $"&keyboard={Uri.EscapeDataString(keyboard)}" +
                                    $"&access_token={token}&v={apiVersion}";

                                await client.GetStringAsync(url);
                                continue;
                            }

                            // 💬 Входящие сообщения
                            if (update.Type == "message_new" && update.Object?.Message != null)
                            {
                                var msg = update.Object.Message.Text ?? "";
                                var userId = update.Object.Message.FromId;

                                Console.WriteLine($"💬 Новое сообщение от {userId}: {msg}");

                                string reply;
                                string? keyboard = null;

                                // Улучшенная обработка категорий билетов
                                if (IsTicketCategoryMessage(msg))
                                {
                                    if (userSelectedData.TryGetValue(userId, out var ticketData))
                                    {
                                        string selectedCategory = GetTicketCategoryFromMessage(msg);
                                        var tariffResult = await GetFormattedTariffsAsync(client, ticketData.date, ticketData.session, selectedCategory);
                                        reply = tariffResult.message;
                                        keyboard = tariffResult.keyboard;
                                    }
                                    else
                                    {
                                        reply = "Сначала выберите дату и сеанс 📅";
                                        keyboard = TicketsDateKeyboard();
                                    }
                                }
                                else
                                {
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
                                            reply = GetWorkingHours();
                                            break;

                                        case "контакты":
                                        case "📞 контакты":
                                            reply = GetContacts();
                                            break;

                                        case "🔙 назад":
                                        case "назад":
                                            reply = "Главное меню:";
                                            keyboard = MainMenuKeyboard();
                                            userSelectedData.TryRemove(userId, out _);
                                            break;

                                        case "🔙 к сеансам":
                                            if (userSelectedData.TryGetValue(userId, out var sessionData))
                                            {
                                                reply = $"Выберите сеанс для даты {sessionData.date}:";
                                                var sessionResult = await GetSessionsForDateAsync(client, sessionData.date);
                                                keyboard = sessionResult.keyboard;
                                            }
                                            else
                                            {
                                                reply = "Выберите дату для сеанса:";
                                                keyboard = TicketsDateKeyboard();
                                            }
                                            break;

                                        case "🔙 в начало":
                                            reply = "Главное меню:";
                                            keyboard = MainMenuKeyboard();
                                            userSelectedData.TryRemove(userId, out _);
                                            break;

                                        case "🎟 купить билеты":
                                        case "билеты":
                                            reply = "Выберите дату для сеанса:";
                                            keyboard = TicketsDateKeyboard();
                                            break;

                                        case "📊 загруженность":
                                        case "загруженность":
                                            reply = await GetParkLoadAsync(client);
                                            break;

                                        default:
                                            if (msg.StartsWith("📅")) // выбор даты
                                            {
                                                string dateStr = msg.Replace("📅", "").Trim();
                                                var sessionResult = await GetSessionsForDateAsync(client, dateStr);
                                                reply = sessionResult.message;
                                                keyboard = sessionResult.keyboard;

                                                // Сохраняем дату для пользователя
                                                userSelectedData[userId] = (dateStr, "");
                                            }
                                            else if (msg.StartsWith("⏰")) // выбор сеанса
                                            {
                                                string sessionTime = msg.Replace("⏰", "").Trim();

                                                // Получаем дату из предыдущего выбора пользователя
                                                if (!userSelectedData.TryGetValue(userId, out var currentData))
                                                {
                                                    reply = "Сначала выберите дату 📅";
                                                    keyboard = TicketsDateKeyboard();
                                                }
                                                else
                                                {
                                                    userSelectedData[userId] = (currentData.date, sessionTime);
                                                    reply = $"🎟 *Сеанс: {sessionTime} ({currentData.date})*\n\nВыберите категорию билетов:";
                                                    keyboard = TicketCategoryKeyboard();
                                                }
                                            }
                                            else
                                            {
                                                reply = "Я вас не понял, попробуйте еще раз 😅";
                                            }
                                            break;
                                    }
                                }

                                string sendUrl =
                                    $"https://api.vk.com/method/messages.send?user_id={userId}" +
                                    $"&random_id={Environment.TickCount}" +
                                    $"&message={Uri.EscapeDataString(reply)}" +
                                    $"&access_token={token}&v={apiVersion}";

                                if (keyboard != null)
                                    sendUrl += $"&keyboard={Uri.EscapeDataString(keyboard)}";

                                await client.GetStringAsync(sendUrl);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Ошибка цикла: {ex.Message}");
                        await Task.Delay(3000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Ошибка при инициализации: {ex.Message}");
            }
        }

        // 🔍 Проверка, является ли сообщение выбором категории билетов
        private static bool IsTicketCategoryMessage(string message)
        {
            var lowerMsg = message.ToLower();
            return lowerMsg.Contains("взрос") ||
                   lowerMsg.Contains("детск") ||
                   lowerMsg.Contains("adult") ||
                   lowerMsg.Contains("child") ||
                   lowerMsg.Contains("kids") ||
                   lowerMsg == "👤" || lowerMsg == "👶" ||
                   lowerMsg == "взрослые" || lowerMsg == "детские" ||
                   lowerMsg == "взрослые билеты" || lowerMsg == "детские билеты" ||
                   lowerMsg == "👤 взрослые" || lowerMsg == "👶 детские" ||
                   lowerMsg == "👤 взрослые билеты" || lowerMsg == "👶 детские билеты";
        }

        // 🔍 Определение категории билетов из сообщения
        private static string GetTicketCategoryFromMessage(string message)
        {
            var lowerMsg = message.ToLower();
            return (lowerMsg.Contains("взрос") || lowerMsg.Contains("adult") || lowerMsg == "👤") ? "adult" : "child";
        }

        // 🎛 Главное меню
        private static string MainMenuKeyboard() => JsonSerializer.Serialize(new
        {
            one_time = false,
            buttons = new[]
            {
                new[] {
                    new { action = new { type = "text", label = "ℹ️ Информация" }, color = "primary" },
                    new { action = new { type = "text", label = "🎟 Купить билеты" }, color = "positive" },
                    new { action = new { type = "text", label = "📊 Загруженность" }, color = "secondary" }
                }
            }
        });

        // ℹ️ Меню информации
        private static string InfoMenuKeyboard() => JsonSerializer.Serialize(new
        {
            one_time = false,
            buttons = new[]
            {
                new[] {
                    new { action = new { type = "text", label = "⏰ Время работы" }, color = "primary" },
                    new { action = new { type = "text", label = "📞 Контакты" }, color = "primary" }
                },
                new[] {
                    new { action = new { type = "text", label = "🔙 Назад" }, color = "negative" }
                }
            }
        });

        // 🎟 Меню выбора даты билетов
        private static string TicketsDateKeyboard()
        {
            var buttons = new object[3][];

            // Первый ряд: сегодня, завтра, послезавтра
            var row1 = new object[3];
            for (int i = 0; i < 3; i++)
            {
                string dateStr = DateTime.Now.AddDays(i).ToString("dd.MM.yyyy");
                row1[i] = new { action = new { type = "text", label = $"📅 {dateStr}" }, color = "primary" };
            }
            buttons[0] = row1;

            // Второй ряд: +3 дня, +4 дня
            var row2 = new object[2];
            for (int i = 3; i < 5; i++)
            {
                string dateStr = DateTime.Now.AddDays(i).ToString("dd.MM.yyyy");
                row2[i - 3] = new { action = new { type = "text", label = $"📅 {dateStr}" }, color = "primary" };
            }
            buttons[1] = row2;

            // Кнопка назад
            buttons[2] = new[] { new { action = new { type = "text", label = "🔙 Назад" }, color = "negative" } };

            return JsonSerializer.Serialize(new { one_time = true, buttons });
        }

        // 🎟 Меню выбора категории билетов (взрослые/детские)
        private static string TicketCategoryKeyboard() => JsonSerializer.Serialize(new
        {
            one_time = true,
            buttons = new[]
            {
                new[] {
                    new { action = new { type = "text", label = "👤 Взрослые билеты" }, color = "primary" },
                    new { action = new { type = "text", label = "👶 Детские билеты" }, color = "positive" }
                },
                new[] {
                    new { action = new { type = "text", label = "🔙 Назад" }, color = "negative" }
                }
            }
        });

        private static string BackKeyboard() => JsonSerializer.Serialize(new
        {
            one_time = true,
            buttons = new[] { new[] { new { action = new { type = "text", label = "🔙 Назад" }, color = "negative" } } }
        });

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
                    return "Не удалось обработать ответ 😔";

                string loadStatus = data.Load switch
                {
                    < 30 => "Мало людей 🟢",
                    < 70 => "Средняя загруженность 🟡",
                    _ => "Много людей 🔴"
                };

                return $"📊 Загруженность аквапарка:\n\n" +
                       $"👥 В данный {data.Count} человек\n" +
                       $"📈 {data.Load}% ({loadStatus})\n\n" +
                       $"💡 Небольшой совет: Лучшее время для посещения, это утром - ведь там будет меньше людей!";
            }
            catch
            {
                return "Ошибка при получении загруженности 😔";
            }
        }

        // 🎟 Получение сеансов для даты с кнопками
        private static async Task<(string message, string keyboard)> GetSessionsForDateAsync(HttpClient client, string date)
        {
            try
            {
                var sessionsUrl = $"https://apigateway.nordciti.ru/v1/aqua/getSessionsAqua?date={date}";
                var sessionsResponse = await client.GetAsync(sessionsUrl);

                if (!sessionsResponse.IsSuccessStatusCode)
                    return ($"⚠️ Ошибка при загрузке сеансов на {date}", TicketsDateKeyboard());

                var sessionsJson = await sessionsResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Сырые данные сеансов: {sessionsJson}");

                var sessionsData = JsonSerializer.Deserialize<JsonElement>(sessionsJson);

                if (!sessionsData.TryGetProperty("result", out var sessionsArray) || sessionsArray.GetArrayLength() == 0)
                    return ($"😔 На {date} нет доступных сеансов.", TicketsDateKeyboard());

                string text = $"🎟 *Доступные сеансы на {date}:*\n\n";
                var buttonsList = new System.Collections.Generic.List<object[]>();

                foreach (var s in sessionsArray.EnumerateArray())
                {
                    string timeStart = s.TryGetProperty("startTime", out var ts) ? ts.GetString() ?? "" : "";
                    string timeEnd = s.TryGetProperty("endTime", out var te) ? te.GetString() ?? "" : "";
                    int placesFree = s.TryGetProperty("availableCount", out var pf) ? pf.GetInt32() : 0;
                    int placesTotal = s.TryGetProperty("totalCount", out var pt) ? pt.GetInt32() : 0;
                    string sessionTime = s.TryGetProperty("sessionTime", out var st) ? st.GetString() ?? $"{timeStart}-{timeEnd}" : $"{timeStart}-{timeEnd}";

                    if (placesFree == 0) continue;

                    string availability = placesFree < 10 ? "🔴 Мало мест!" : "🟢 Есть места";
                    text += $"⏰ *{sessionTime}* | {availability}\n";
                    text += $"   Свободно: {placesFree}/{placesTotal} мест\n\n";

                    buttonsList.Add(new object[]
                    {
                        new { action = new { type = "text", label = $"⏰ {sessionTime}" }, color = "primary" }
                    });
                }

                if (buttonsList.Count == 0)
                    return ($"😔 На {date} нет свободных мест.", TicketsDateKeyboard());

                buttonsList.Add(new object[]
                {
                    new { action = new { type = "text", label = "🔙 Назад" }, color = "negative" }
                });

                string keyboard = JsonSerializer.Serialize(new { one_time = true, buttons = buttonsList });
                return (text, keyboard);
            }
            catch (Exception ex)
            {
                return ($"Ошибка при получении сеансов 😔\n{ex.Message}", TicketsDateKeyboard());
            }
        }

        // 🎟 Получение и форматирование тарифов по категориям
        private static async Task<(string message, string keyboard)> GetFormattedTariffsAsync(HttpClient client, string date, string sessionTime, string category)
        {
            try
            {
                var tariffsUrl = $"https://apigateway.nordciti.ru/v1/aqua/getTariffsAqua?date={date}";
                var tariffsResponse = await client.GetAsync(tariffsUrl);

                if (!tariffsResponse.IsSuccessStatusCode)
                    return ($"⚠️ Ошибка при загрузке тарифов", BackKeyboard());

                var tariffsJson = await tariffsResponse.Content.ReadAsStringAsync();
                var tariffsData = JsonSerializer.Deserialize<JsonElement>(tariffsJson);

                if (!tariffsData.TryGetProperty("result", out var tariffsArray) || tariffsArray.GetArrayLength() == 0)
                    return ($"⚠️ Не удалось получить тарифы", BackKeyboard());

                string categoryTitle = category == "adult" ? "👤 ВЗРОСЛЫЕ БИЛЕТЫ" : "👶 ДЕТСКИЕ БИЛЕТЫ";
                string text = $"🎟 *{categoryTitle}*\n";
                text += $"⏰ Сеанс: {sessionTime}\n";
                text += $"📅 Дата: {date}\n\n";

                var filteredTariffs = new System.Collections.Generic.List<(string name, decimal price)>();
                var seenTariffs = new System.Collections.Generic.HashSet<string>();

                foreach (var t in tariffsArray.EnumerateArray())
                {
                    string name = t.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                    decimal price = t.TryGetProperty("Price", out var p) ? p.GetDecimal() : 0;

                    if (string.IsNullOrEmpty(name))
                        name = t.TryGetProperty("name", out var n2) ? n2.GetString() ?? "" : "";

                    if (price == 0)
                        price = t.TryGetProperty("price", out var p2) ? p2.GetDecimal() : 0;

                    // Создаем уникальный ключ для избежания дубликатов
                    string tariffKey = $"{name.ToLower()}_{price}";

                    if (seenTariffs.Contains(tariffKey)) continue;
                    seenTariffs.Add(tariffKey);

                    // Улучшенная фильтрация по категории
                    string nameLower = name.ToLower();
                    bool isAdult = nameLower.Contains("взрос") ||
                                  nameLower.Contains("adult") ||
                                  (nameLower.Contains("вип") && !nameLower.Contains("дет")) ||
                                  (nameLower.Contains("взр") && !nameLower.Contains("дет")) ||
                                  (price > 1000 && !nameLower.Contains("дет"));

                    bool isChild = nameLower.Contains("детск") ||
                                  nameLower.Contains("child") ||
                                  nameLower.Contains("kids") ||
                                  nameLower.Contains("дет") ||
                                  (price < 1000 && nameLower.Contains("билет") && !nameLower.Contains("взр"));

                    if ((category == "adult" && isAdult && !isChild) ||
                        (category == "child" && isChild && !isAdult))
                    {
                        filteredTariffs.Add((name, price));
                    }
                }

                if (filteredTariffs.Count == 0)
                {
                    text += "😔 Нет доступных билетов этой категории\n";
                    text += "💡 Попробуйте выбрать другую категорию";
                }
                else
                {
                    // Группируем и сортируем билеты
                    var groupedTariffs = filteredTariffs
                        .GroupBy(t => FormatTicketName(t.name))
                        .Select(g => g.First())
                        .OrderByDescending(t => t.price)
                        .ToList();

                    foreach (var (name, price) in groupedTariffs)
                    {
                        string emoji = price > 2000 ? "💎 VIP" : price > 1000 ? "⭐ Стандарт" : "🎫 Эконом";
                        string formattedName = FormatTicketName(name);
                        text += $"{emoji} *{formattedName}*: {price}₽\n";
                    }

                    text += $"\n💡 Примечания:\n";
                    text += $"• Детский билет - для детей от 4 до 12 лет\n";
                    text += $"• Дети до 4 лет - бесплатно (с взрослым)\n";
                    text += $"• VIP билеты включают дополнительные услуги";
                }

                text += $"\n🔗 *Купить онлайн:* yes35.ru";

                string keyboard = JsonSerializer.Serialize(new
                {
                    one_time = false,
                    buttons = new object[][]
                    {
                        new object[]
                        {
                            new { action = new { type = "open_link", link = "https://yes35.ru/aquapark/tickets", label = "🎟 Купить на сайте" } }
                        },
                        new object[]
                        {
                            new { action = new { type = "text", label = "👤 Взрослые" }, color = category == "adult" ? "positive" : "primary" },
                            new { action = new { type = "text", label = "👶 Детские" }, color = category == "child" ? "positive" : "primary" }
                        },
                        new object[]
                        {
                            new { action = new { type = "text", label = "🔙 К сеансам" }, color = "secondary" },
                            new { action = new { type = "text", label = "🔙 В начало" }, color = "negative" }
                        }
                    }
                });

                return (text, keyboard);
            }
            catch (Exception ex)
            {
                return ($"Ошибка при получении тарифов 😔\n{ex.Message}", BackKeyboard());
            }
        }

        // 📝 Форматирование названий билетов
        private static string FormatTicketName(string name)
        {
            var formatted = name
                .Replace("Билет", "")
                .Replace("билет", "")
                .Replace("Вип", "VIP")
                .Replace("весь день", "Весь день")
                .Replace("взрослый", "")
                .Replace("детский", "")
                .Replace("вечерний", "Вечерний")
                .Replace("  ", " ")
                .Trim();

            // Убираем лишние пробелы и дублирования
            if (formatted.StartsWith("VIP") || formatted.StartsWith("Вип"))
            {
                formatted = "VIP" + formatted.Substring(3).Trim();
            }

            return string.IsNullOrEmpty(formatted) ? "Стандартный" : formatted;
        }

        // ⏰ Время работы
        private static string GetWorkingHours()
        {
            return "🏢 Режим работы точек Центра YES:\n\n" +

                   "🌊 Аквапарк\n" +
                   "⏰ 10:00 - 21:00 │ 📅 Ежедневно\n" +
                   "💧 Бассейны, горки, сауны\n\n" +

                   "🍽️ Ресторан\n" +
                   "⏰ 10:00 - 21:00 │ 📅 Ежедневно\n" +
                   "🍕 Кухня европейская и азиатская\n\n" +

                   "🎮 Игровой центр\n" +
                   "⏰ 10:00 - 18:00 │ 📅 Ежедневно\n" +
                   "🎯 Автоматы и симуляторы\n\n" +

                   "🦖 Динопарк\n" +
                   "⏰ 10:00 - 18:00 │ 📅 Ежедневно\n" +
                   "🦕 Интерактивные экспонаты\n\n" +

                   "🏨 Гостиница\n" +
                   "⏰ Круглосуточно │ 📅 Ежедневно\n" +
                   "🛏️ Номера различных категорий\n\n" +

                   "🔴 Временно не работают:\n" +
                   "• 🧗‍ Веревочный парк\n" +
                   "• 🧗‍ Скалодром\n" +
                   "• 🎡 Парк аттракционов\n" +
                   "• 🍔 MasterBurger\n\n" +

                   "📞 *Уточнить информацию:* (8172) 33-06-06";
        }

        // 📞 Контакты
        private static string GetContacts()
        {
            return "📞 Контакты Центра YES\n\n" +

                    "📱 Телефон для связи:\n" +
                    "• Основной: (8172) 33-06-06\n" +
                    "• Ресторан: 8-800-200-67-71\n\n" +

                    "📧 Электронная почта:\n" +
                    "yes@yes35.ru\n\n" +

                    "🌐 Мы в соцсетях:\n" +
                    "ВКонтакте: vk.com/yes35\n" +
                    "Telegram: t.me/CentreYES35\n" +
                    "WhatsApp: ссылка в профиле\n\n" +

                    "⏰ Часы работы call-центра:\n" +
                    "🕙 09:00 - 22:00";
        }

        // 🧱 Модели данных
        public class ParkLoadResponse { public int Count { get; set; } public int Load { get; set; } }
        public class SessionResponse { public SessionItem[] Data { get; set; } = Array.Empty<SessionItem>(); }
        public class SessionItem
        {
            [JsonPropertyName("TimeStart")] public string TimeStart { get; set; } = "";
            [JsonPropertyName("TimeEnd")] public string TimeEnd { get; set; } = "";
            [JsonPropertyName("PlacesFree")] public int PlacesFree { get; set; }
            [JsonPropertyName("PlacesTotal")] public int PlacesTotal { get; set; }
        }
        public class TariffResponse { public TariffItem[] Data { get; set; } = Array.Empty<TariffItem>(); }
        public class TariffItem
        {
            [JsonPropertyName("Name")] public string Name { get; set; } = "";
            [JsonPropertyName("Price")] public decimal Price { get; set; }
        }
    }

    // 🔹 VK API модели
    public class LongPollServerResponse { public LongPollServer Response { get; set; } = null!; }
    public class LongPollServer { public string Key { get; set; } = null!; public string Server { get; set; } = null!; public string Ts { get; set; } = null!; }
    public class LongPollUpdate { public string Ts { get; set; } = null!; public UpdateItem[] Updates { get; set; } = Array.Empty<UpdateItem>(); }
    public class UpdateItem { public string Type { get; set; } = null!; public UpdateObject? Object { get; set; } }
    public class UpdateObject { [JsonPropertyName("message")] public MessageItem? Message { get; set; } [JsonPropertyName("user_id")] public long? UserId { get; set; } }
    public class MessageItem
    {
        public string Text { get; set; } = "";
        [JsonPropertyName("from_id")]
        public long FromId { get; set; }
    }
}