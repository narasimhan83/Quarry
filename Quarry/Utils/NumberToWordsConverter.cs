using System;

namespace QuarryManagementSystem.Utils
{
    public static class NumberToWordsConverter
    {
        private static readonly string[] Units = { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
        private static readonly string[] Teens = { "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        private static readonly string[] Tens = { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
        private static readonly string[] Thousands = { "", "Thousand", "Million", "Billion", "Trillion" };

        public static string ConvertAmountToWords(decimal amount)
        {
            if (amount == 0)
                return "Zero Naira";

            var naira = (long)Math.Floor(amount);
            var kobo = (int)((amount - naira) * 100);

            var words = "";

            if (naira > 0)
            {
                words = ConvertNumberToWords(naira) + " Naira";
            }

            if (kobo > 0)
            {
                if (naira > 0)
                    words += " and ";
                words += ConvertNumberToWords(kobo) + " Kobo";
            }

            return words + " Only";
        }

        private static string ConvertNumberToWords(long number)
        {
            if (number == 0)
                return "Zero";

            if (number < 0)
                return "Minus " + ConvertNumberToWords(Math.Abs(number));

            string words = "";

            for (int i = 0; number > 0; i++)
            {
                if (number % 1000 != 0)
                {
                    words = ConvertHundreds((int)(number % 1000)) + " " + Thousands[i] + " " + words;
                }
                number /= 1000;
            }

            return words.Trim();
        }

        private static string ConvertHundreds(int number)
        {
            string words = "";

            if (number >= 100)
            {
                words += Units[number / 100] + " Hundred";
                number %= 100;
                if (number > 0)
                    words += " and ";
            }

            if (number >= 20)
            {
                words += Tens[number / 10];
                number %= 10;
                if (number > 0)
                    words += " " + Units[number];
            }
            else if (number >= 10)
            {
                words += Teens[number - 10];
            }
            else if (number > 0)
            {
                words += Units[number];
            }

            return words;
        }
    }
}