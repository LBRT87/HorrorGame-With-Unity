using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
public class EncryptionGameData
{
    private static readonly string keyBit = "1234567890123456";
    public static string Enkripsi(string teks)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(keyBit);
            aes.IV = Encoding.UTF8.GetBytes(keyBit);

            ICryptoTransform icryptotransform = aes.CreateEncryptor(aes.Key, aes.IV);
            using (MemoryStream memoristream = new MemoryStream())
            using (CryptoStream cryptostream = new CryptoStream(memoristream, icryptotransform, CryptoStreamMode.Write))
                using (StreamWriter streamwriter = new StreamWriter(cryptostream))
            {
                streamwriter.Write(teks);
                streamwriter.Close();

                return Convert.ToBase64String(memoristream.ToArray());
            }
        }
    }

   public static string Dekripsi (string teks)
   {
        using (Aes des = Aes.Create())
        {
            des.Key = Encoding.UTF8.GetBytes(keyBit);
            des.IV = Encoding.UTF8.GetBytes(keyBit);
            ICryptoTransform icryptotransform = des.CreateDecryptor(des.Key, des.IV);

            using (MemoryStream memorystream = new MemoryStream(Convert.FromBase64String(teks)))
            using (CryptoStream cryptostream = new CryptoStream(memorystream, icryptotransform, CryptoStreamMode.Read))
                using (StreamReader sr = new StreamReader(cryptostream))
            {
                return sr.ReadToEnd();
            }
        }
          
   }             
     
   
}
