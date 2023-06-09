namespace Cloud.Data
{
    using System.ComponentModel.DataAnnotations;

    public class LoginModel
    {
        [Required]
#if DEBUG
        string _qr = "AglxnU03pY3Ng27Z7epOQYfisk4kUBuhQCnAy3wtxIrldGVzdA==";
        public string QR { get { return _qr; } set { _qr = value; } }
#else 
        public string QR { get; set; }
#endif

        [Required]
        [StringLength(10, ErrorMessage = "Name is too long.")]

#if DEBUG
        string _pin = "777777";
        public string Pin { get { return _pin; } set { _pin = value; } }
#else 
        public string Pin { get; set; }
#endif

    }
}
