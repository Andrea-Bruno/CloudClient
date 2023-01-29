namespace Cloud.Data
{
    using System.ComponentModel.DataAnnotations;

    public class LoginModel
    {
        [Required]
#if DEBUG
        string _qr = "AdfSvKH4FjcC8wvDP6L196UnRm0U+SO5rTIt+mPFO0AgRYt22+LSmei3CYcI9F5gA5llh7W/dNhefbmjLmxJCFzK2pEcf3vJfhaOgVk1M59OLh60EgqNk555wOrROZNHZtB08XtzTiJXwXG/SVCJZpvQyCKtW+IuqZ3EXNFf51qP80UqEreUDldU/uaEZdPBDJue0AamX8/QSjyGcU52pk8dxq0YMjAwm7Q7zBBttNf0k4m0Tiw8P/GfSDJBsmwOi4rsl+eF7wPaPWx6GlHTjqTms5/01y8cbPm4XlLoLcU4Z5L685NE9JQS3iPT8BeIGbPWek/KXEprZL9d3COy7nEBAAECk/54JE+v2Rv8lKvP9ogZqf3uye4VhmxZxa7l7PKvmrJodHRwOi8vMjA5LjIwOS40MS4xNTU6NTA1MA==";
        public string? QR { get { return _qr; } set { _qr = value; } }
#else 
        public string? QR { get; set; }
#endif

        [Required]
        [StringLength(10, ErrorMessage = "Name is too long.")]

#if DEBUG
        string _pin = "181499";
        public string? Pin { get { return _pin; } set { _pin = value; } }
#else 
        public string? Pin { get; set; }
#endif

    }
}
