using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace KEnergy_Library
{
    // типы сообщений
    public enum messageType
    {
        // данные
        Data,
        // подключение клиента
        Join,
        // начало передачи
        TransmissionBegin,
        // конец передачи
        TransmissionEnd,
        // неизвестно
        Unknown
    }

    // класс входных данных для каждой квартиры
    public class EnergyInput
    {
        // значения по оси времени
        private List<int> timeValues = new List<int>() { 0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24 };
        // значения по оси энергопотребления
        public List<double> energyValues;

        // базовый конструктор
        public EnergyInput(List<double> _evergyValues)
        {
            energyValues = _evergyValues;
            energyValues.Add(energyValues.First());
        }

        // поиск значения энергопотребления в заданный момент времени
        public double getEnergyValue(double timeValue)
        {
            List<int> tValues = null;
            List<double> eValues = null;
            // если timeValue не принадлежит отрезку [0; 24]
            if (timeValue < 0 || timeValue > 24)
                return -1;
            // поиск двух ближайших граничных для timeValue значений timeValues_m и timeValues_n из массива timeValues
            for (int i = 0; i < 12; i++)
                if (timeValues[i] <= timeValue && timeValues[i + 1] >= timeValue)
                {
                    tValues = new List<int>() { timeValues[i], timeValues[i + 1] };
                    eValues = new List<double>() { energyValues[i], energyValues[i + 1] };
                }
            // интерполяция полиномами Лагранжа 2-й степени на участке timeValues_m <= timeValue <= timeValues_n и поиск значения в точке timeValue
            double energyVal = 0;
            for (int i = 0; i < 2; i++)
            {
                double energyBasicVal = 1;
                for (int j = 0; j < 2; j++)
                {
                    if (j != i)
                    {
                        energyBasicVal *= (timeValue - tValues[j]) / (tValues[i] - tValues[j]);
                    }
                }
                energyVal += energyBasicVal * eValues[i];
            }
            return energyVal;
        }
    }

    // класс дополнительных методов
    public static class AdditionalComponents
    {
        // метод получения локального IP-адреса
        public static string LocalIPAddress()
        {
            // локальный адрес
            string localIP = "";
            // получаем IP-адреса компьютера в локальной сети
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                // если это IPv4-адрес
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    // сохраняем его
                    localIP = ip.ToString();
                    break;
                }
            }
            // возвращаем его
            return localIP;
        }

        // конвертация типа сообщения из строки в перечисление
        public static messageType stringToMessageType(string type)
        {
            switch (type)
            {
                case "Data":
                    return messageType.Data;
                case "Join":
                    return messageType.Join;
                case "TransmissionBegin":
                    return messageType.TransmissionBegin;
                case "TransmissionEnd":
                    return messageType.TransmissionEnd;
            }
            return messageType.Unknown;
        }
    }
}
