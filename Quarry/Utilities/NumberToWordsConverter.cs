namespace QuarryManagementSystem.Utilities
{
    public static class NumberToWordsConverter
    {
        private static readonly string[] Units = { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", 
                                                  "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        
        private static readonly string[] Tens = { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
        
        private static readonly string[] Thousands = { "", "Thousand", "Million", "Billion", "Trillion" };

        public static string ConvertToWords(decimal amount)
        {
            if (amount == 0)
                return "Zero Naira";

            if (amount < 0)
                return "Minus " + ConvertToWords(Math.Abs(amount));

            string words = "";
            long naira = (long)amount;
            int kobo = (int)((amount - naira) * 100);

            // Convert Naira
            if (naira > 0)
            {
                words = ConvertNumberToWords(naira) + " Naira";
            }

            // Convert Kobo
            if (kobo > 0)
            {
                if (naira > 0)
                    words += " and ";
                words += ConvertNumberToWords(kobo) + " Kobo";
            }

            return words.Trim();
        }

        private static string ConvertNumberToWords(long number)
        {
            if (number == 0)
                return "Zero";

            if (number < 0)
                return "Minus " + ConvertNumberToWords(Math.Abs(number));

            string words = "";

            if ((number / 1000000000000) > 0)
            {
                words += ConvertNumberToWords(number / 1000000000000) + " Trillion ";
                number %= 1000000000000;
            }

            if ((number / 1000000000) > 0)
            {
                words += ConvertNumberToWords(number / 1000000000) + " Billion ";
                number %= 1000000000;
            }

            if ((number / 1000000) > 0)
            {
                words += ConvertNumberToWords(number / 1000000) + " Million ";
                number %= 1000000;
            }

            if ((number / 1000) > 0)
            {
                words += ConvertNumberToWords(number / 1000) + " Thousand ";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += ConvertNumberToWords(number / 100) + " Hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                if (words != "")
                    words += "and ";

                if (number < 20)
                {
                    words += Units[number];
                }
                else
                {
                    words += Tens[number / 10];
                    if ((number % 10) > 0)
                    {
                        words += "-" + Units[number % 10];
                    }
                }
            }

            return words.Trim();
        }

        public static string ConvertAmountToWords(decimal amount, string currency = "Naira", string subCurrency = "Kobo")
        {
            if (amount == 0)
                return $"Zero {currency}";

            if (amount < 0)
                return "Minus " + ConvertAmountToWords(Math.Abs(amount), currency, subCurrency);

            string words = "";
            long mainAmount = (long)amount;
            int subAmount = (int)((amount - mainAmount) * 100);

            // Convert main currency
            if (mainAmount > 0)
            {
                words = ConvertNumberToWords(mainAmount) + $" {currency}";
            }

            // Convert sub currency
            if (subAmount > 0)
            {
                if (mainAmount > 0)
                    words += " and ";
                words += ConvertNumberToWords(subAmount) + $" {subCurrency}";
            }

            return words.Trim();
        }

        public static string ConvertToWords(int number)
        {
            return ConvertNumberToWords(number);
        }

        public static string ConvertToWords(long number)
        {
            return ConvertNumberToWords(number);
        }

        // Helper method for invoice amounts
        public static string ConvertInvoiceAmount(decimal amount)
        {
            string words = ConvertToWords(amount);
            return char.ToUpper(words[0]) + words.Substring(1) + " Only";
        }

        // Helper method for payroll amounts
        public static string ConvertSalaryAmount(decimal amount)
        {
            return ConvertAmountToWords(amount, "Naira", "Kobo");
        }

        // Helper method for cheque amounts
        public static string ConvertChequeAmount(decimal amount)
        {
            string words = ConvertToWords(amount);
            return "**" + words.ToUpper() + "**";
        }
    }
}