using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
    static string root = Path.Combine(AppContext.BaseDirectory, "ChatData");
    static string fileStorage = Path.Combine(root, "files");
    static string avatars = Path.Combine(root, "avatars");

    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(fileStorage);
        Directory.CreateDirectory(avatars);

        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== ConsoleChat ===");
            Console.WriteLine("1. Локальный чат на одном компьютере");
            Console.WriteLine("2. Запустить TCP-сервер");
            Console.WriteLine("3. Подключиться к TCP-серверу");
            Console.WriteLine("0. Выход");
            Console.Write("Выбор: ");

            string choice = Console.ReadLine() ?? "";

            if (choice == "1") LocalMode();
            else if (choice == "2") await ServerMode();
            else if (choice == "3") await ClientMode();
            else if (choice == "0") return;
        }
    }

    static void LocalMode()
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== Локальный режим ===");
            Console.WriteLine("1. Войти");
            Console.WriteLine("2. Создать профиль");
            Console.WriteLine("0. Назад");
            Console.Write("Выбор: ");

            string choice = Console.ReadLine() ?? "";

            if (choice == "1")
            {
                User user = Login();
                if (user != null)
                {
                    user.IsOnline = true;
                    SaveUser(user);
                    UserMenu(user);
                    user.IsOnline = false;
                    SaveUser(user);
                }
            }
            else if (choice == "2") Register();
            else if (choice == "0") return;
        }
    }

    static void Register()
    {
        List<User> users = Load<List<User>>("users.json") ?? new List<User>();

        Console.Clear();
        Console.WriteLine("=== Создание профиля ===");
        Console.Write("Логин: ");
        string login = (Console.ReadLine() ?? "").Trim();
        Console.Write("Пароль: ");
        string password = ReadPassword();

        if (login == "" || password == "")
        {
            Pause("Логин и пароль не должны быть пустыми.");
            return;
        }

        if (users.Any(u => u.Login.Equals(login, StringComparison.OrdinalIgnoreCase)))
        {
            Pause("Такой пользователь уже существует.");
            return;
        }

        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        User user = new User
        {
            Id = Guid.NewGuid(),
            Login = login,
            Hash = MakeHash(password, salt),
            Salt = salt,
            AvatarPath = "",
            IsOnline = false
        };

        users.Add(user);
        Save("users.json", users);

        List<Chat> chats = Load<List<Chat>>("chats.json") ?? new List<Chat>();
        chats.Add(new Chat
        {
            Id = Guid.NewGuid(),
            Name = "Избранное",
            Type = "favorites",
            Members = new List<Guid> { user.Id },
            Messages = new List<Message>()
        });
        Save("chats.json", chats);

        Pause("Профиль создан. Чат 'Избранное' добавлен автоматически.");
    }

    static User Login()
    {
        List<User> users = Load<List<User>>("users.json") ?? new List<User>();

        Console.Clear();
        Console.WriteLine("=== Вход ===");
        Console.Write("Логин: ");
        string login = (Console.ReadLine() ?? "").Trim();
        Console.Write("Пароль: ");
        string password = ReadPassword();

        User user = users.FirstOrDefault(u => u.Login.Equals(login, StringComparison.OrdinalIgnoreCase));

        if (user == null || user.Hash != MakeHash(password, user.Salt))
        {
            Pause("Неверный логин или пароль.");
            return null;
        }

        return user;
    }

    static void UserMenu(User currentUser)
    {
        while (true)
        {
            Console.Clear();
            currentUser = GetFreshUser(currentUser.Id) ?? currentUser;
            Console.WriteLine("=== Профиль: " + currentUser.Login + " ===");
            Console.WriteLine("Фото профиля: " + (string.IsNullOrWhiteSpace(currentUser.AvatarPath) ? "нет" : currentUser.AvatarPath));
            Console.WriteLine("1. Мои чаты");
            Console.WriteLine("2. Создать личный чат");
            Console.WriteLine("3. Создать групповой чат");
            Console.WriteLine("4. Перейти в другой профиль");
            Console.WriteLine("5. Добавить/заменить фото профиля");
            Console.WriteLine("6. Удалить фото профиля");
            Console.WriteLine("7. Посмотреть фото пользователя");
            Console.WriteLine("0. Выйти из профиля");
            Console.Write("Выбор: ");

            string choice = Console.ReadLine() ?? "";

            if (choice == "1") ShowChats(currentUser);
            else if (choice == "2") CreateDirectChat(currentUser);
            else if (choice == "3") CreateGroupChat(currentUser);
            else if (choice == "4")
            {
                currentUser.IsOnline = false;
                SaveUser(currentUser);
                User newUser = Login();
                if (newUser != null)
                {
                    newUser.IsOnline = true;
                    SaveUser(newUser);
                    currentUser = newUser;
                }
                else
                {
                    currentUser.IsOnline = true;
                    SaveUser(currentUser);
                }
            }
            else if (choice == "5") AddAvatar(currentUser);
            else if (choice == "6") DeleteAvatar(currentUser);
            else if (choice == "7") ShowUserAvatar();
            else if (choice == "0") return;
        }
    }

    static void AddAvatar(User currentUser)
    {
        Console.Write("Путь к фото профиля: ");
        string path = (Console.ReadLine() ?? "").Trim('"');

        if (!File.Exists(path))
        {
            Pause("Файл не найден.");
            return;
        }

        string extension = Path.GetExtension(path);
        string newPath = Path.Combine(avatars, currentUser.Id + extension);
        File.Copy(path, newPath, true);

        currentUser.AvatarPath = newPath;
        SaveUser(currentUser);
        Pause("Фото профиля добавлено.");
    }

    static void DeleteAvatar(User currentUser)
    {
        if (!string.IsNullOrWhiteSpace(currentUser.AvatarPath) && File.Exists(currentUser.AvatarPath))
            File.Delete(currentUser.AvatarPath);

        currentUser.AvatarPath = "";
        SaveUser(currentUser);
        Pause("Фото профиля удалено.");
    }

    static void ShowUserAvatar()
    {
        List<User> users = Load<List<User>>("users.json") ?? new List<User>();
        Console.Write("Логин пользователя: ");
        string login = (Console.ReadLine() ?? "").Trim();
        User user = users.FirstOrDefault(u => u.Login.Equals(login, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            Pause("Пользователь не найден.");
            return;
        }

        if (string.IsNullOrWhiteSpace(user.AvatarPath) || !File.Exists(user.AvatarPath))
        {
            Pause("У пользователя нет фото профиля.");
            return;
        }

        Console.WriteLine("Фото профиля пользователя " + user.Login + ":");
        Console.WriteLine(user.AvatarPath);
        Console.WriteLine("Скопируйте путь и откройте файл через Проводник Windows.");
        Pause("Готово.");
    }

    static void ShowChats(User currentUser)
    {
        while (true)
        {
            List<User> users = Load<List<User>>("users.json") ?? new List<User>();
            List<Chat> chats = Load<List<Chat>>("chats.json") ?? new List<Chat>();
            List<Chat> myChats = chats.Where(c => c.Members.Contains(currentUser.Id)).ToList();

            Console.Clear();
            Console.WriteLine("=== Мои чаты ===");

            for (int i = 0; i < myChats.Count; i++)
            {
                string status = GetChatOnlineStatus(myChats[i], users, currentUser.Id);
                Console.WriteLine((i + 1) + ". " + myChats[i].Name + " [" + myChats[i].Type + "] " + status);
            }

            Console.WriteLine("0. Назад");
            Console.Write("Выбор: ");

            int number;
            if (!int.TryParse(Console.ReadLine(), out number)) continue;
            if (number == 0) return;
            if (number >= 1 && number <= myChats.Count) OpenChat(currentUser, myChats[number - 1].Id);
        }
    }

    static string GetChatOnlineStatus(Chat chat, List<User> users, Guid currentUserId)
    {
        if (chat.Type == "favorites") return "";

        List<User> members = users.Where(u => chat.Members.Contains(u.Id) && u.Id != currentUserId).ToList();
        int onlineCount = members.Count(u => u.IsOnline);

        if (chat.Type == "direct")
        {
            User other = members.FirstOrDefault();
            if (other == null) return "";
            return other.IsOnline ? "[online]" : "[offline]";
        }

        return "[online: " + onlineCount + "/" + members.Count + "]";
    }

    static void OpenChat(User currentUser, Guid chatId)
    {
        while (true)
        {
            List<User> users = Load<List<User>>("users.json") ?? new List<User>();
            List<Chat> chats = Load<List<Chat>>("chats.json") ?? new List<Chat>();
            Chat chat = chats.First(c => c.Id == chatId);

            Console.Clear();
            Console.WriteLine("=== " + chat.Name + " ===");
            Console.WriteLine("Статус: " + GetChatOnlineStatus(chat, users, currentUser.Id));
            Console.WriteLine("Команды: /file - файл, /emoji - emoji, /history - история, /photo логин - фото, /back - назад");
            Console.WriteLine(new string('-', 60));

            foreach (Message msg in chat.Messages.TakeLast(30))
                PrintMessage(msg, users);

            Console.WriteLine(new string('-', 60));
            Console.Write("Сообщение: ");
            string text = Console.ReadLine() ?? "";

            if (text.Equals("/back", StringComparison.OrdinalIgnoreCase)) return;

            if (text.Equals("/history", StringComparison.OrdinalIgnoreCase))
            {
                ShowHistory(chat, users);
                continue;
            }

            if (text.Equals("/emoji", StringComparison.OrdinalIgnoreCase))
            {
                text = ChooseEmoji();
                if (text == "") continue;
            }

            if (text.StartsWith("/photo ", StringComparison.OrdinalIgnoreCase))
            {
                string login = text.Substring(7).Trim();
                ShowAvatarByLogin(login);
                continue;
            }

            if (text.Equals("/file", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write("Путь к файлу: ");
                string path = (Console.ReadLine() ?? "").Trim('"');

                if (!File.Exists(path))
                {
                    Pause("Файл не найден.");
                    continue;
                }

                string savedPath = Path.Combine(fileStorage, Guid.NewGuid().ToString() + "_" + Path.GetFileName(path));
                File.Copy(path, savedPath, true);

                chat.Messages.Add(new Message
                {
                    From = currentUser.Id,
                    Time = DateTime.Now,
                    Text = "[Файл]",
                    FilePath = savedPath
                });
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                chat.Messages.Add(new Message
                {
                    From = currentUser.Id,
                    Time = DateTime.Now,
                    Text = text,
                    FilePath = null
                });
            }

            Save("chats.json", chats);
        }
    }

    static void PrintMessage(Message msg, List<User> users)
    {
        User senderUser = users.FirstOrDefault(u => u.Id == msg.From);
        string sender = senderUser == null ? "Неизвестно" : senderUser.Login;
        Console.WriteLine("[" + msg.Time.ToString("dd.MM.yyyy HH:mm") + "] " + sender + ": " + msg.Text);

        if (!string.IsNullOrWhiteSpace(msg.FilePath))
            Console.WriteLine("  Файл: " + msg.FilePath);
    }

    static void ShowHistory(Chat chat, List<User> users)
    {
        Console.Clear();
        Console.WriteLine("=== Полная история: " + chat.Name + " ===");
        Console.WriteLine(new string('-', 60));

        foreach (Message msg in chat.Messages)
            PrintMessage(msg, users);

        Pause("Конец истории.");
    }

    static string ChooseEmoji()
    {
        Console.WriteLine("Выберите emoji:");
        Console.WriteLine("1. 😀  2. 😂  3. 😎  4. ❤️  5. 👍  6. 🔥  7. 🎉  8. 😢");
        Console.Write("Номер: ");
        string choice = Console.ReadLine() ?? "";

        if (choice == "1") return "😀";
        if (choice == "2") return "😂";
        if (choice == "3") return "😎";
        if (choice == "4") return "❤️";
        if (choice == "5") return "👍";
        if (choice == "6") return "🔥";
        if (choice == "7") return "🎉";
        if (choice == "8") return "😢";
        return "";
    }

    static void ShowAvatarByLogin(string login)
    {
        List<User> users = Load<List<User>>("users.json") ?? new List<User>();
        User user = users.FirstOrDefault(u => u.Login.Equals(login, StringComparison.OrdinalIgnoreCase));

        if (user == null)
        {
            Pause("Пользователь не найден.");
            return;
        }

        if (string.IsNullOrWhiteSpace(user.AvatarPath) || !File.Exists(user.AvatarPath))
        {
            Pause("У пользователя нет фото профиля.");
            return;
        }

        Console.WriteLine("Фото профиля " + user.Login + ": " + user.AvatarPath);
        Pause("Скопируйте путь и откройте файл через Проводник Windows.");
    }

    static void CreateDirectChat(User currentUser)
    {
        List<User> users = Load<List<User>>("users.json") ?? new List<User>();
        List<Chat> chats = Load<List<Chat>>("chats.json") ?? new List<Chat>();

        Console.Write("Логин собеседника: ");
        string login = (Console.ReadLine() ?? "").Trim();
        User otherUser = users.FirstOrDefault(u => u.Login.Equals(login, StringComparison.OrdinalIgnoreCase));

        if (otherUser == null || otherUser.Id == currentUser.Id)
        {
            Pause("Пользователь не найден.");
            return;
        }

        chats.Add(new Chat
        {
            Id = Guid.NewGuid(),
            Name = currentUser.Login + " - " + otherUser.Login,
            Type = "direct",
            Members = new List<Guid> { currentUser.Id, otherUser.Id },
            Messages = new List<Message>()
        });

        Save("chats.json", chats);
        Pause("Личный чат создан.");
    }

    static void CreateGroupChat(User currentUser)
    {
        List<User> users = Load<List<User>>("users.json") ?? new List<User>();

        Console.Write("Название группы: ");
        string name = (Console.ReadLine() ?? "").Trim();

        Console.Write("Логины участников через запятую: ");
        string[] logins = (Console.ReadLine() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);

        List<Guid> members = new List<Guid>();
        members.Add(currentUser.Id);

        foreach (string rawLogin in logins)
        {
            string login = rawLogin.Trim();
            User user = users.FirstOrDefault(u => u.Login.Equals(login, StringComparison.OrdinalIgnoreCase));
            if (user != null && !members.Contains(user.Id)) members.Add(user.Id);
        }

        if (name == "" || members.Count < 2)
        {
            Pause("Для группы нужно название и хотя бы один другой участник.");
            return;
        }

        List<Chat> chats = Load<List<Chat>>("chats.json") ?? new List<Chat>();
        chats.Add(new Chat
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = "group",
            Members = members,
            Messages = new List<Message>()
        });

        Save("chats.json", chats);
        Pause("Групповой чат создан.");
    }

    static async Task ServerMode()
    {
        Console.Clear();
        Console.Write("Порт сервера, например 7777: ");

        int port;
        if (!int.TryParse(Console.ReadLine(), out port)) port = 7777;

        List<StreamWriter> clients = new List<StreamWriter>();
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();

        Console.WriteLine("Сервер запущен на порту " + port + ".");
        Console.WriteLine("Чтобы остановить сервер, закройте окно программы.");

        while (true)
        {
            TcpClient tcpClient = await server.AcceptTcpClientAsync();

            _ = Task.Run(async () =>
            {
                using StreamReader reader = new StreamReader(tcpClient.GetStream(), Encoding.UTF8);
                using StreamWriter writer = new StreamWriter(tcpClient.GetStream(), Encoding.UTF8) { AutoFlush = true };

                lock (clients) clients.Add(writer);

                try
                {
                    string line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        List<StreamWriter> copy;
                        lock (clients) copy = clients.ToList();

                        foreach (StreamWriter client in copy)
                        {
                            try { await client.WriteLineAsync(line); }
                            catch { }
                        }
                    }
                }
                finally
                {
                    lock (clients) clients.Remove(writer);
                }
            });
        }
    }

    static async Task ClientMode()
    {
        Console.Clear();
        Console.Write("IP сервера: ");
        string ip = (Console.ReadLine() ?? "127.0.0.1").Trim();

        Console.Write("Порт: ");
        int port;
        if (!int.TryParse(Console.ReadLine(), out port)) port = 7777;

        Console.Write("Ваше имя: ");
        string name = (Console.ReadLine() ?? "Гость").Trim();

        using TcpClient tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(ip, port);

        using StreamReader reader = new StreamReader(tcpClient.GetStream(), Encoding.UTF8);
        using StreamWriter writer = new StreamWriter(tcpClient.GetStream(), Encoding.UTF8) { AutoFlush = true };

        string downloads = Path.Combine(root, "network_downloads");
        Directory.CreateDirectory(downloads);

        _ = Task.Run(async () =>
        {
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                try
                {
                    NetworkMessage msg = JsonSerializer.Deserialize<NetworkMessage>(line);
                    if (msg == null) continue;

                    if (!string.IsNullOrWhiteSpace(msg.FileName) && !string.IsNullOrWhiteSpace(msg.FileBase64))
                    {
                        string savePath = Path.Combine(downloads, Guid.NewGuid().ToString() + "_" + msg.FileName);
                        File.WriteAllBytes(savePath, Convert.FromBase64String(msg.FileBase64));
                        Console.WriteLine();
                        Console.WriteLine("[" + msg.Chat + "] " + msg.From + " отправил файл: " + savePath);
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("[" + msg.Chat + "] " + msg.From + ": " + msg.Text);
                    }

                    Console.Write("> ");
                }
                catch
                {
                }
            }
        });

        string currentChat = "Общий";

        Console.WriteLine("Команды:");
        Console.WriteLine("/chat имя_чата - сменить сетевой чат");
        Console.WriteLine("/file путь_к_файлу - отправить файл");
        Console.WriteLine("/emoji - отправить emoji");
        Console.WriteLine("/exit - выйти");

        while (true)
        {
            Console.Write("> ");
            string text = Console.ReadLine() ?? "";

            if (text.Equals("/exit", StringComparison.OrdinalIgnoreCase)) return;

            if (text.Equals("/emoji", StringComparison.OrdinalIgnoreCase))
            {
                text = ChooseEmoji();
                if (text == "") continue;
            }

            if (text.StartsWith("/chat ", StringComparison.OrdinalIgnoreCase))
            {
                currentChat = text.Substring(6).Trim();
                Console.WriteLine("Текущий чат: " + currentChat);
                continue;
            }

            if (text.StartsWith("/file ", StringComparison.OrdinalIgnoreCase))
            {
                string path = text.Substring(6).Trim('"');

                if (!File.Exists(path))
                {
                    Console.WriteLine("Файл не найден.");
                    continue;
                }

                NetworkMessage fileMessage = new NetworkMessage
                {
                    From = name,
                    Chat = currentChat,
                    Text = "[Файл]",
                    FileName = Path.GetFileName(path),
                    FileBase64 = Convert.ToBase64String(File.ReadAllBytes(path))
                };

                await writer.WriteLineAsync(JsonSerializer.Serialize(fileMessage));
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                NetworkMessage textMessage = new NetworkMessage
                {
                    From = name,
                    Chat = currentChat,
                    Text = text,
                    FileName = null,
                    FileBase64 = null
                };

                await writer.WriteLineAsync(JsonSerializer.Serialize(textMessage));
            }
        }
    }

    static User GetFreshUser(Guid id)
    {
        List<User> users = Load<List<User>>("users.json") ?? new List<User>();
        return users.FirstOrDefault(u => u.Id == id);
    }

    static void SaveUser(User changedUser)
    {
        List<User> users = Load<List<User>>("users.json") ?? new List<User>();
        int index = users.FindIndex(u => u.Id == changedUser.Id);
        if (index >= 0) users[index] = changedUser;
        Save("users.json", users);
    }

    static T Load<T>(string fileName)
    {
        string path = Path.Combine(root, fileName);
        if (!File.Exists(path)) return default(T);
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json);
    }

    static void Save<T>(string fileName, T data)
    {
        string path = Path.Combine(root, fileName);
        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(path, json);
    }

    static string MakeHash(string password, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(hashBytes);
    }

    static string ReadPassword()
    {
        string password = "";

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password;
            }

            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password.Substring(0, password.Length - 1);
                Console.Write(" ");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
                Console.Write("*");
            }
        }
    }

    static void Pause(string text)
    {
        Console.WriteLine(text);
        Console.WriteLine("Нажмите Enter...");
        Console.ReadLine();
    }
}

class User
{
    public Guid Id { get; set; }
    public string Login { get; set; }
    public string Hash { get; set; }
    public string Salt { get; set; }
    public string AvatarPath { get; set; }
    public bool IsOnline { get; set; }
}

class Chat
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public List<Guid> Members { get; set; }
    public List<Message> Messages { get; set; }
}

class Message
{
    public Guid From { get; set; }
    public DateTime Time { get; set; }
    public string Text { get; set; }
    public string FilePath { get; set; }
}

class NetworkMessage
{
    public string From { get; set; }
    public string Chat { get; set; }
    public string Text { get; set; }
    public string FileName { get; set; }
    public string FileBase64 { get; set; }
}
