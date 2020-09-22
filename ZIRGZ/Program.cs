using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FiatShamir
{
    [Serializable]

    public class User
    {
        public string Username { get; set; }
        public PublicKey PublicKey { get; set; }

        public User(string username, PublicKey publicKey)
        {
            Username = username;
            PublicKey = publicKey;
        }
    }

    // Класс для генерации публичного ключа с помошью хеширования пароля
    [Serializable]
    public class PublicKey
    {
        public BigInteger Value { get; set; }

        public PublicKey(BigInteger value) { Value = value; }

        
        public static PublicKey FromPassword(string password, BigInteger n)
        {
            var s = Program.Hash(password);
            return new PublicKey(s * s % n);
        }
    }

    class Program
    {
        
        static RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        static SHA512 sha = SHA512.Create();
        static Random random = new Random();

        #region Utils


        
        static BigInteger BigRandom(int bytes)
        {
            var b = new byte[bytes];
            rng.GetBytes(b);
            return new BigInteger(b, true);
        }

        static BigInteger BigRandomWithin(BigInteger min, BigInteger max)
        {
            if (min > max)
            {
                throw new ArgumentException("min > max");
            }
            BigInteger a = BigRandom(max.GetByteCount());
            if (a >= min && a < max)
            {
                return a;
            }
            var t = a % (max - min) + min;
            return t;
        }

        
        static BigInteger GeneratePrime(int bytes)
        {
            BigInteger a;
            do
            {
                a = BigRandom(bytes);
            } while (!IsPrime(a));
            return a;
        }

        
        static bool IsPrime(BigInteger n, int k = 15)
        {
            if (n == 2 || n == 3)
                return true;
            if (n < 2 || n % 2 == 0)
                return false;

            BigInteger t = n - 1;
            int s = 0;
            while (t % 2 == 0)
            {
                t /= 2;
                s += 1;
            }

            for (int i = 0; i < k; i++)
            {
                BigInteger a;
                do
                {
                    a = BigRandom(n.GetByteCount());
                }
                while (a < 2 || a >= n - 2);

                BigInteger x = BigInteger.ModPow(a, t, n);

                if (x == 1 || x == n - 1)
                    continue;

                for (int r = 1; r < s; r++)
                {
                    x = BigInteger.ModPow(x, 2, n);
                    if (x == 1)
                        return false;
                    if (x == n - 1)
                        break;
                }

                if (x != n - 1)
                    return false;
            }
            return true;
        }

        #endregion

        #region Server Repository

        //Класс для загрузки и хранения значений сервером
        class Sstorage
        {
            private string usersFilename;
            private string nFilename;
            private List<User> users;

            public BigInteger N { get; private set; }

            public Sstorage(string usersFilename, string nFilename)
            {
                this.usersFilename = usersFilename;
                this.nFilename = nFilename;
                //Генерация N
                N = LoadOrGenerateN();
                users = LoadUsers();
            }

            public void AddUser(User user)
            {
                users.Add(user);
                SaveUsers(users);
            }

            public User FindUser(string username) => users.Find(u => u.Username == username);

            private void SaveUsers(List<User> users)
            {
                using var file = new FileStream(usersFilename, FileMode.Create, FileAccess.Write);
                new BinaryFormatter().Serialize(file, users);
            }

            private List<User> LoadUsers()
            {
                if (!File.Exists(usersFilename))
                {
                    return new List<User>();
                }
                using var file = new FileStream(usersFilename, FileMode.Open, FileAccess.Read);
                return (List<User>)new BinaryFormatter().Deserialize(file);
            }

            
            private BigInteger LoadOrGenerateN()
            {
                if (File.Exists(nFilename))
                {
                    return new BigInteger(File.ReadAllBytes(nFilename), true);
                }
                BigInteger q = GeneratePrime(1024 / 8);
                BigInteger p = GeneratePrime(1024 / 8);
                BigInteger n = p * q;
                File.WriteAllBytes(nFilename, n.ToByteArray());
                return n;
            }
        }

        #endregion

       //Хеширование
        public static BigInteger Hash(string s)
        {
            byte[] strBytes = Encoding.UTF8.GetBytes(s);
            byte[] hash = sha.ComputeHash(strBytes);
            byte[] hashX3 = hash.Concat(hash).Concat(hash).ToArray();
            return new BigInteger(hashX3, true);
        }

        //Серверная сторона
        class Server
        {
            private const int NumRounds = 20;

           
            private Sstorage storage = new Sstorage("users.dat", "nfile.dat");

            public BigInteger N => storage.N;

            
            public void Regs(User user)
            {
               //Console.WriteLine($"{user.Username}'s public key: {user.PublicKey.Value}");
                storage.AddUser(user);
            }

           
            public bool Ins(string username, Func<Numxr> s_sn)
            {
                var user = storage.FindUser(username);
                if (user == null)
                {
                    Console.WriteLine($"Пользователь не найден");
                    return false;
                }

                Console.WriteLine($"{user.Username}'s public key: {user.PublicKey.Value}");

                BigInteger v = user.PublicKey.Value;
                for (int i = 0; i < NumRounds; i++)
                {
                    Console.WriteLine($"\n\n\n\nРаунд {i + 1}/{NumRounds}");

                    Numxr round = s_sn();

                    BigInteger x = round.X;
                    Console.WriteLine("X: " + x);

                    int e = random.Next(0, 2);
                    Console.WriteLine("E = " + e);

                    BigInteger y = round.GenY(e);
                    Console.WriteLine("Y: " + y);

                    if (y == 0)
                    {
                        return false;
                    }

                    BigInteger left = y * y % N;
                    if (e == 1)
                    {
                        BigInteger right = x * v % N;
                       
                        if (left != right)
                        {
                            Console.WriteLine($"Левая и правая части не равны");
                            return false;
                        }
                        else
                        {
                            Console.WriteLine($"Раунд прошел");
                        }
                    }
                    else
                    {
                        BigInteger right = x;
                        
                        if (left != right)
                        {
                            Console.WriteLine($"Левая и правая части не равны");
                            return false;
                        }
                        else
                        {
                            Console.WriteLine($"Раунд прошел");
                        }
                    }
                }
                
                return true;
            }
        }

        //Клиентская сторона
        class Client
        {
            
            private Server server;

            public Client(Server server)
            {
                this.server = server;
            }

            
            public void Go()
            {
                while (true)
                {
                    Console.WriteLine("1 - Войти");
                    Console.WriteLine("2 - Зарегистрироваться");
                    Console.WriteLine("3 - Выхол");

                    string str = Console.ReadLine();
                    int option = Convert.ToInt32(str);

                    Console.WriteLine();
                    switch (option)
                    {
                        case 1: In(); break;
                        case 2: Reg(); break;
                        case 3: return;
                    }
                }
            }

            
            private void In()
            {
                Console.WriteLine("Введите логин: ");
                string username = Console.ReadLine();
                Console.WriteLine("Введите пароль: ");
                string password = Console.ReadLine();

                BigInteger s = Hash(password);
                
                bool success = server.Ins(username, () => new Numxr(s, server.N));

                Console.WriteLine(success ? "Успешно вошли!" : "Не смогли войти!");
                Console.WriteLine("\n");
            }

            
            private void Reg()
            {
                Console.WriteLine("Введите логин: ");
                string username = Console.ReadLine();
                Console.WriteLine("Введите пароль: ");
                string password = Console.ReadLine();

                var n = server.N;
                var publicKey = PublicKey.FromPassword(password, n);
                server.Regs(new User(username, publicKey));

                Console.WriteLine("Успешно зарегестрировались!\n\n");
            }
        }

        //Класс для получения чисел
        class Numxr
        {
            
            private BigInteger n;
            
            private BigInteger s;
            
            private BigInteger r;

            
            public BigInteger X { get; }

            public Numxr(BigInteger s, BigInteger n)
            {
                this.n = n;
                this.s = s;

                r = BigRandomWithin(1, n - 1);
                Console.WriteLine("R: " + r);
                X = r * r % n;
            }

            
            public BigInteger GenY(int e)
            {
                if (e == 1) {
                    return (r * s % n); 
                }
                else
                {
                    return r;
                }
            }
        }

        static void Main(string[] args)
        {
            var server = new Server();
            var client = new Client(server);
            client.Go();
        }
    }
}
