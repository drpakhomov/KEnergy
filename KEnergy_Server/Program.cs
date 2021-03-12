using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using KEnergy_Library;

namespace KEnergy_Server
{
    public class Program
    {
        // сгенерированные данные
        public static List<EnergyInput> generatedData = new List<EnergyInput>();
        // время генерации на клиентской стороне
        public static double totalClientTime = 0;
        // флаги завершения передачи клиентами
        public static List<bool> doneByClients = new List<bool>();
        // хост для отправки данных
        public static IPAddress serverAddress = IPAddress.Parse("235.16.4.99");
        // порт для отправки данных
        public const int remotePort = 8001;
        // локальный порт для прослушивания входящих подключений
        public const int localPort = 8002;
        // адреса клиентов
        public static List<string> clientAddresses = new List<string>();
        // счетчики: количество массивов для генерации, количество подключишихся клиентов, ожидаемое количество клиентов
        public static int count = 0, clientCount = 0, clientCountMax = 0;
        // флаг завершения работы
        public static bool done = false;

        public static void Main(string[] args)
        {
            Console.Title = "Генерация массивов данных: серверная часть";
            Console.WindowWidth += 10;

            // очистка файла с данными
            StreamWriter sw = new StreamWriter("generatedData.txt", false);
            sw.Close();

            // запрашиваем с клавиатуры ввод количества клиентов
            Console.Write(">> Введите количество клиентов: ");
            clientCountMax = int.Parse(Console.ReadLine());

            // запуск прослушки
            try
            {
                listenAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("UDP >> " + ex.Message);
            }
            
            // инициализация массива флагов
            for (int i = 0; i < clientCountMax; i++)
                doneByClients.Add(false);

            Console.WriteLine(">> Ожидаем подключения всех клиентов...");

            // ожидаем завершения работы потока
            Thread.CurrentThread.Join();
        }

        // метод отправки сообщений
        private static void SendMessage(string msg, messageType type, string receiver)
        {
            // UdpClient для отправки
            UdpClient udp = new UdpClient();
            // конечная точка
            IPEndPoint endPoint = new IPEndPoint(serverAddress, remotePort);
            try
            {
                // формирование сообщения определенной структуры: сообщение~тип~получатель
                string message = msg;
                message += "~" + type.ToString();
                message += "~" + receiver;
                // переводим его в массив байтов
                byte[] data = Encoding.Unicode.GetBytes(message);
                // отправка массива байтов
                int sended = udp.Send(data, data.Length, endPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine("UDP >> " + ex.Message);
            }
            finally
            {
                udp.Close();
            }
        }

        // прослушка
        public static void listenAsync()
        {
            Task.Run(async () =>
            {
                // создаем UdpClient для прослушки на локальном порте
                using (var udpClient = new UdpClient(localPort))
                {
                    // подключаемся к широковещательной рассылке
                    udpClient.JoinMulticastGroup(serverAddress);
                    while (!done)
                    {
                        // получение сообщения
                        var receivedResults = await udpClient.ReceiveAsync();
                        // обработка
                        await Task.Run(() => handleResults(receivedResults.Buffer, receivedResults.RemoteEndPoint));
                    }
                }
            });
        }

        // метод обработки сообщений
        public static void handleResults(byte[] data, IPEndPoint remoteIp)
        {
            // если мы не закончили работу
            if (!done)
            {
                // отправитель
                string sender = remoteIp.Address.ToString();
                // полученное сообщение
                string message = Encoding.Unicode.GetString(data);
                string[] msgArr = message.Split('~');
                // тип сообщения
                messageType type = AdditionalComponents.stringToMessageType(msgArr[1]);
                // текст сообщения
                message = msgArr[0];
                // получатель сообщения
                string _receiver = msgArr[2];
                // если тип сообщения - Join (клиент подключился)
                if (type == messageType.Join)
                {
                    // добавляем адрес клиента в список
                    clientAddresses.Add(sender);
                    // увеличиваем счетчик количества подключившихся клиентов
                    clientCount++;
                    // сообщаем о подключении
                    Console.WriteLine("UDP >> Клиент " + sender + " успешно подключен!");
                    // если все клиенты подключились
                    if (clientCount == clientCountMax)
                    {
                        // ввод с клавиатуры количества массивов для генерации
                        Console.Write(">> Введите количество массивов, которые нужно сгенерировать: ");
                        count = int.Parse(Console.ReadLine());
                        // отправляем клиентам сообщение о необходимости начала генерации (количество массивов делим на количество клиентов)
                        if (clientAddresses.Count > 0)
                        {
                            //Console.WriteLine(">> Отправка запросов генерации " + (1.1 * count / clientCountMax) + " (+10%) массивов...");
                            Console.WriteLine(">> Отправка запросов генерации " + (count / clientCountMax) + " массивов...");

                            for (int i = 0; i < clientAddresses.Count; i++)
                                //SendMessage((1.1 * count / clientCount).ToString() + "|" + i.ToString(), messageType.TransmissionBegin, clientAddresses[i]);
                                SendMessage((count / clientCount).ToString() + "|" + i.ToString(), messageType.TransmissionBegin, clientAddresses[i]);
                        }
                    }
                }
                // если мы - получатели
                if (_receiver == AdditionalComponents.LocalIPAddress())
                {
                    // если тип сообщения - Data (получили сгенерированные массивы)
                    if (type == messageType.Data)
                    {
                        // разбиение полученного пакета на отдельные (каждый кусок соответствует одному сгенерированному массиву)
                        string[] energyDataStr = message.Split('/');
                        for (int i = 0; i < energyDataStr.Length - 1; i++)
                        {
                            // содержимое каждого сгенерированного массива
                            List<double> energyData = new List<double>();
                            // извлечение данных из сообщения
                            string[] energyValues = energyDataStr[i].Split('|');
                            // конвертация
                            for (int j = 0; j < energyValues.Length - j; j++)
                                energyData.Add(Convert.ToDouble(energyValues[j]));
                            // добавление сгенерированного массива в список
                            generatedData.Add(new EnergyInput(energyData));
                            // сохранение в файл
                            StreamWriter sw = new StreamWriter("generatedData.txt", true);
                            sw.WriteLine(energyDataStr[i].Replace('|', ';'));
                            sw.Close();
                        }
                        Console.WriteLine("UDP >> Получен пакет данных от " + sender + " длиной " + data.Length + " байт; массивов в пакете: " + (energyDataStr.Length - 1));
                    }
                    // если тип сообщения - TransmissionEnd (сообщение об окончании передачи)
                    else if (type == messageType.TransmissionEnd)
                    {
                        // извлекаем из сообщения время генерации
                        double time = Math.Round(Convert.ToDouble(message), 2);
                        // если этот клиент потратил на генерацию больше времени, чем какой-либо другой, то записываем это время как новое
                        if (time > totalClientTime) totalClientTime = time;
                        // отмечаем в массиве флагов, что клиент закончил генерацию
                        doneByClients[clientAddresses.IndexOf(sender)] = true;

                        Console.WriteLine("UDP >> Клиент " + sender + " завершил передачу данных, сгенерированных за " + time + " секунд!");

                        // проверяем, все ли клиенты закончили генерацию
                        int c = 0;
                        foreach (bool client in doneByClients)
                            if (client)
                                c++;

                        // если все клиенты закончили
                        if (c == clientAddresses.Count)
                        {
                            // отмечаем флаг завершения работы
                            done = true;
                            /*double lost = 1.1 * count - generatedData.Count();
                            double lostp = 110 * lost / generatedData.Count();*/
                            // выводим информационные сообщения
                            Console.WriteLine(">> Все клиенты завершили передачу данных!");
                            Console.WriteLine(">> Сгенерировано массивов: " + count);
                            //Console.WriteLine(">> Сгенерировано массивов: " + generatedData.Count() + " / " + count);
                            //Console.WriteLine(">> Потеряно при передаче: " + Math.Round(lost, 0) + " (" + Math.Round(lostp, 2) + "%)");
                            /*if (generatedData.Count > count)
                            {
                                Console.WriteLine(">> Компенсационные излишки: " + (generatedData.Count() - count));
                                generatedData.RemoveRange(count, generatedData.Count - count);
                                Console.WriteLine(">> Итого массивов: " + generatedData.Count());
                            }*/
                            Console.WriteLine(">> Потрачено времени на выполнение: " + Math.Round(totalClientTime, 2) + " секунд");
                            Console.WriteLine(">> Нажмите любую клавишу для выхода...");
                            // выход из программы
                            Console.ReadKey();
                            Environment.Exit(0);
                        }
                    }
                }
            }
        }
    }
}