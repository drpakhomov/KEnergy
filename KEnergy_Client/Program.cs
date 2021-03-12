using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using KEnergy_Library;

namespace KEnergy_Client
{
    public class Program
    {
        // базовые данные для генерации
        public static List<double> avgDataMonFri = new List<double>() { 12.1, 6.5, 6.3, 12.1, 34.1, 39.9, 31.3, 32.5, 36.5, 55.2, 57.1, 43.8 };
        // количество потоков
        public const int threadCount = 4;
        // хост для отправки данных
        public static IPAddress serverAddress = IPAddress.Parse("235.16.4.99");
        // порт для отправки данных
        public const int remotePort = 8002;
        // локальный порт для прослушивания входящих подключений
        public const int localPort = 8001;
        // адрес сервера
        public static string serverIP = "";
        // потоки: прослушки, завершения работы потоков генерации
        public static Thread receiveThread = null, finishThread = new Thread(new ThreadStart(isFinished));
        // замок
        public static object lock1 = new object();
        // список потоков генерации
        public static List<Thread> threadList = new List<Thread>();
        // количество массивов для генерации, количество сгенерированных массивов, порядковый номер клиента
        public static int count = 0, tot = 0, clientNum = 0, packetCount = 0;
        // флаг прерывания
        public static bool abortFlag = false;
        // рандомизатор
        public static Random rnd = new Random();

        public static void Main(string[] args)
        {
            Console.Title = "Генерация массивов данных: клиентская часть";

            // запуск прослушки
            try
            {
                receiveThread = new Thread(new ThreadStart(ReceiveMessage));
                receiveThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("UDP >> " + ex.Message);
            }

            // отправка сообщения о появлении клиента
            SendMessage("", messageType.Join, "");
            Console.WriteLine(">> Клиент готов к генерации данных, ожидание запроса...");
        }

        // запуск генерации
        public static void beginGeneration()
        {
            // разбиваем количество массивов, которые надо сгенерировать, на количество потоков, и запускаем их
            for (int i = 0; i < threadCount; i++)
                threadList.Add(new Thread(runGenerator));
            foreach (Thread thread in threadList)
                thread.Start();

            // запуск потока ожидания
            finishThread.Start();
            finishThread.Join();
        }

        public static void isFinished()
        {
            // начальный момент времени
            long elapsedTime = DateTime.Now.Hour * 3600000 + DateTime.Now.Minute * 60000 + DateTime.Now.Second * 1000 + DateTime.Now.Millisecond;
            // количество неработающих потоков
            int c = 0;
            // пока количество неработающих потоков не станет равно общему числу потоков
            while (c < threadList.Count)
            {
                c = 0;
                foreach (Thread t in threadList)
                    if (!t.IsAlive)
                        c++;
            }
            // если все потоки завершили работу
            if (c == threadList.Count)
            {
                // конечный момент времени
                elapsedTime = DateTime.Now.Hour * 3600000 + DateTime.Now.Minute * 60000 + DateTime.Now.Second * 1000 + DateTime.Now.Millisecond - elapsedTime;
                // вывод информационных сообщений
                //Console.WriteLine(">> Сгенерировано наборов: " + tot);
                Console.WriteLine(">> Сгенерировано наборов: " + count);
                Console.WriteLine(">> Потрачено времени на выполнение: " + Math.Round((elapsedTime / 1000.0) - packetCount, 2) + " секунд");
                // отправка на сервер сообщения об окончании передачи
                SendMessage(((elapsedTime / 1000.0) - packetCount).ToString(), messageType.TransmissionEnd, serverIP);
                // отмечаем флаг прерывания
                abortFlag = true;
            }
        }

        // запуск генерации массивов
        public static void runGenerator()
        {
            generateData(avgDataMonFri, count / threadCount);
        }

        // метод генерации массивов
        public static void generateData(List<double> sourceData, int count)
        {
            // сдвиг начала работы (чтобы разные клиенты не отправляли сообщения одновременно)
            Thread.Sleep(clientNum * 7500);

            List<List<double>> resultedList = new List<List<double>>();
            List<EnergyInput> normalizedResultedList = new List<EnergyInput>();

            List<double> props = new List<double>(), propData = new List<double>() { 0 };
            List<int> peaks = new List<int>() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            double sum = sourceData.Sum();
            foreach (double num in sourceData)
                props.Add(Math.Round(100 * num / sum, 1));
            double n = 0;
            foreach (double num in props)
            {
                n += num;
                propData.Add(n);
            }
            for (int j = 0; j < count * 10; j++)
            {
                double chance = 0;
                lock (lock1)
                {
                    chance = 100 * rnd.NextDouble();
                }
                for (int i = 0; i < propData.Count - 1; i++)
                    if (chance > propData[i] && chance <= propData[i + 1])
                        peaks[i]++;
            }
            List<double> countOfPeaks = new List<double>() { 0, 88, 93, 96, 98, 99, 99.5, 99.9, 100 };
            while (peaks.Sum() > 0)
            {
                int numOfPeaks = 0;
                double chance = 0;
                lock (lock1)
                {
                    chance = 100 * rnd.NextDouble();
                }
                for (int i = 0; i < countOfPeaks.Count - 1; i++)
                    if (chance > countOfPeaks[i] && chance <= countOfPeaks[i + 1])
                        numOfPeaks = i + 1;
                if (numOfPeaks > peaks.Sum()) numOfPeaks = peaks.Sum();
                List<double> list = new List<double>() { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                for (int i = 0; i < numOfPeaks; i++)
                {
                    int peak = 0;
                    lock (lock1)
                    {
                        peak = rnd.Next(0, 12);
                    }
                    if (list[peak] == 0)
                        list[peak]++;
                    else
                        numOfPeaks++;
                }
                for (int i = 0; i < peaks.Count; i++)
                    if (list[i] == 1)
                        peaks[i]--;
                resultedList.Add(list);
            }
            foreach (List<double> list in resultedList)
            {
                double max = sourceData.Max() / list.Sum();
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == 1)
                    {
                        double vary = 0;
                        int chance = 0;
                        lock (lock1)
                        {
                            vary = rnd.Next(1, 5) * rnd.NextDouble();
                            chance = rnd.Next(0, 2);
                        }
                        if (chance == 0) max += vary; else max -= vary;
                        list[i] = max;
                    }
                    else
                    {
                        double value = sourceData[i];
                        double vary = 0;
                        int chance = 0;
                        lock (lock1)
                        {
                            vary = rnd.Next(1, (int)sourceData[i]) * rnd.NextDouble();
                            chance = rnd.Next(0, 2);
                        }
                        if (chance == 0) value += vary; else value -= vary;
                        list[i] = value;
                    }
                }
            }
            for (int i = 0; i < resultedList.Count; i++)
            {
                int c = 0;
                for (int j = 0; j < 11; j++)
                {
                    if (resultedList[i][j] / resultedList[i][j + 1] > 3 || resultedList[i][j + 1] / resultedList[i][j] > 3)
                        continue;
                    else
                        c++;
                }
                if (c == 11)
                {
                    for (int j = 0; j < 12; j++)
                        resultedList[i][j] /= 101;
                    normalizedResultedList.Add(new EnergyInput(resultedList[i]));
                }
            }
            if (normalizedResultedList.Count > count)
                normalizedResultedList.RemoveRange(count, normalizedResultedList.Count - count);

            // отправка данных пакетами по 315 массивов
            // сообщение
            string msg = "";
            // количество сообщений
            int totalMessages = 0;
            // пройдемся по всем сгенерированным массивам
            foreach (EnergyInput EI in normalizedResultedList)
            {
                // добавим к сообщению каждый элемент массива + разделитель
                foreach (double en in EI.energyValues)
                    msg += Math.Round(en, 5) + "|";
                // добавим разделитель между соседними массивами
                msg += "/";
                // увеличим счетчик сообщений
                totalMessages++;
                // если в сообщение добавили 315 массивов, и текущий массив - не последний в списке
                if (totalMessages == 315 && EI != normalizedResultedList.Last())
                {
                    Thread.Sleep(13500);
                    // не позволяем нескольким потокам отправлять сообщения одновременно (т.к. сервер не сможет их обработать)
                    lock (lock1)
                    {
                        // задержка между отправкой сообщений
                        Thread.Sleep(1500);
                        // отправка сформированного сообщения
                        SendMessage(msg, messageType.Data, serverIP);
                        packetCount++;
                    }
                    // сброс сообщения и счетчика
                    msg = "";
                    totalMessages = 0;
                }
                // если текущий массив - последний в списке
                if (EI == normalizedResultedList.Last())
                {
                    Thread.Sleep(13500);
                    // не позволяем нескольким потокам отправлять сообщения одновременно (т.к. сервер не сможет их обработать)
                    lock (lock1)
                    {
                        // задержка между отправкой сообщений
                        Thread.Sleep(1500);
                        // отправка сформированного сообщения (которое будет содержать менее 315 массивов)
                        SendMessage(msg, messageType.Data, serverIP);
                        packetCount++;
                    }
                    // сброс сообщения и счетчика
                    msg = "";
                    totalMessages = 0;
                }
                // увеличение счетчика сгенерированных массивов
                lock (lock1)
                {
                    tot++;
                }
            }
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
                // если передаются массивы, то выводим размер пакета на экран
                if (type == messageType.Data)
                    Console.WriteLine("UDP >> Отправка пакета данных размером " + data.Length + " байт для " + serverIP + "...");
                // отправка массива байтов
                int sended = udp.Send(data, data.Length, endPoint);
                if (type == messageType.Data)
                    Console.WriteLine("UDP >> Пакет успешно отправлен");
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

        // метод обработки сообщений
        private static void ReceiveMessage()
        {
            // UdpClient для получения данных
            UdpClient receiver = new UdpClient(localPort);
            // подключаемся к широковещательной рассылке
            receiver.JoinMulticastGroup(serverAddress, 20);
            // адрес отправителя
            IPEndPoint remoteIp = null;
            try
            {
                // бесконечный цикл
                while (true)
                {
                    // получение сообщения в виде массива байтов
                    byte[] data = receiver.Receive(ref remoteIp);
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
                    // мы - получатели
                    if (_receiver == AdditionalComponents.LocalIPAddress())
                    {
                        // если тип сообщения - TransmissionBegin (начало передачи)
                        if (type == messageType.TransmissionBegin)
                        {
                            // сохраняем IP-адрес сервера
                            serverIP = sender;
                            // извлекаем из сообщения количество массивов для генерации и порядковый номер клиента
                            string[] msg = message.Split('|');
                            count = Convert.ToInt32(msg[0]);
                            clientNum = Convert.ToInt32(msg[1]);
                            // выводим информационное сообщение
                            Console.WriteLine("UDP >> Получен запрос генерации " + count + " массивов данных от " + sender + ", запуск");
                            // начинаем генерацию
                            beginGeneration();
                        }
                    }

                    // если все потоки завершили работу
                    if (abortFlag)
                    {
                        Console.WriteLine(">> Нажмите любую клавишу для выхода...");
                        Console.ReadKey();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("UDP >> " + ex.Message);
            }
            finally
            {
                receiver.Close();
            }
        }
    }   
}