using System.Collections.Generic;

namespace dm.YLD.TelegramBot
{
    public class Config
    {
        public string BotToken { get; set; }
        public bool BotWatch { get; set; }
        public long[] ChatIds { get; set; }
        public long AdminId { get; set; }
    }
}
