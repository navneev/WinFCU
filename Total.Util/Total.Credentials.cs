using System;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Total.Util
{
    public partial class total
    {
        private static string Rotate13(string Record)
        {
            // --------------------------------------------------------------------------------------------------------------------
            //   Use a Rotate13 approach to 'encrypt'/'decrypt' a string
            // --------------------------------------------------------------------------------------------------------------------
            string Seed = @"PxT+ZHtWso(vrN5IcbKLq U%pF!m7OkfJ4Agwd2D)3-CyhG.Yz^S/XMj=E6Bnla@i*u_VeR\0#Q8$19";
            int seedLength = Seed.Length; string rotatedRecord = null;
            foreach (char s in Record.ToCharArray()) { int i = Seed.IndexOf(s) + 1; rotatedRecord += Seed[seedLength - i]; }
            return rotatedRecord;
        }
        private static string ByteArrayToHexString(byte[] ba)
        {
            string hex = BitConverter.ToString(ba);
            return hex.Replace("-", "");
        }
        private static byte[] HexStringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
        // ========================================================================================================================
        //   Verify whether given account exists. Valid account formats are: user, machine\user, domain\user, user@domain
        // ------------------------------------------------------------------------------------------------------------------------
        public static bool AccountExists(string checkAccount)
        {
            string accountDomain;
            string accountName;
            PrincipalContext pc;
            if (checkAccount.StartsWith(@"NT AUTHORITY\")) { Logger.Warn("Will not operate on NT AUTHORITY accounts"); return false; }
            // --------------------------------------------------------------------------------------------------------------------
            //   Split account into domain(machine) and username part. 
            // --------------------------------------------------------------------------------------------------------------------
            string[] accountParts = checkAccount.Split('\\');
            if (accountParts.Length == 2) { accountDomain = accountParts[0]; accountName = accountParts[1]; }
            else
            {
                accountParts = checkAccount.Split('@');
                if (accountParts.Length == 2) { accountDomain = accountParts[1]; accountName = accountParts[0]; }
                else { accountDomain = "."; accountName = accountParts[0]; }
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   Find the account principal context (local or domain account) 
            // --------------------------------------------------------------------------------------------------------------------
            if ((accountDomain == ".") ||
                (accountDomain == "BUILTIN") ||
                (accountDomain == Environment.MachineName)) { pc = new PrincipalContext(ContextType.Machine); }
            else { pc = new PrincipalContext(ContextType.Domain, accountDomain); }
            // --------------------------------------------------------------------------------------------------------------------
            //   Find the account identity and return found (true) or not found (false)
            // --------------------------------------------------------------------------------------------------------------------
            Principal accountPrincipal = Principal.FindByIdentity(pc, IdentityType.SamAccountName, accountName);
            return (accountPrincipal != null);
        }

        // ========================================================================================================================
        //   Encrypt supplied credentials to a single encrypted string of characters
        // ------------------------------------------------------------------------------------------------------------------------
        public static string EncryptCredentials(string Account, string Password)
        {
            byte[] accBase = new byte[256]; byte[] pwdBase = new byte[256];
            string hashedAccount = "", hashedPassword = "", hashedCredentials = "";
            string tokenV4 = "enc-V4-aes:";
            Random rnd = new Random();
            // --------------------------------------------------------------------------------------------------------------------
            //   Use the RNGCryptoServiceProvider to generate a random account/password encryption seed (well kind of...)
            // --------------------------------------------------------------------------------------------------------------------
            new RNGCryptoServiceProvider().GetBytes(accBase);
            new RNGCryptoServiceProvider().GetBytes(pwdBase);
            // --------------------------------------------------------------------------------------------------------------------
            //   Use the Rotate13 function etc. to 'hash' both the account and the password
            // --------------------------------------------------------------------------------------------------------------------
            hashedAccount = ByteArrayToHexString(Encoding.UTF8.GetBytes(Rotate13(Account)));
            hashedPassword = ByteArrayToHexString(Encoding.UTF8.GetBytes(Rotate13(Password)));
            // --------------------------------------------------------------------------------------------------------------------
            //   Save lengths and offsets in the seedBase
            // --------------------------------------------------------------------------------------------------------------------
            byte accOffset = accBase[0] = (byte)rnd.Next(4, 64); accBase[1] = (byte)hashedAccount.Length;
            byte pwdOffset = pwdBase[0] = (byte)rnd.Next(4, 64); pwdBase[1] = (byte)hashedPassword.Length;
            // --------------------------------------------------------------------------------------------------------------------
            //   Use the hashed account & password to create an encryption credential string
            // --------------------------------------------------------------------------------------------------------------------
            foreach (byte c in hashedAccount)  { accBase[accOffset++] = (byte)c; }
            foreach (byte c in hashedPassword) { pwdBase[pwdOffset++] = (byte)c; }
            hashedCredentials = ByteArrayToHexString(accBase) + ByteArrayToHexString(pwdBase);
            // --------------------------------------------------------------------------------------------------------------------
            //   Prefix the credentialstring with the token and return the set
            // --------------------------------------------------------------------------------------------------------------------
            return (tokenV4 + hashedCredentials);
        }

        // ========================================================================================================================
        //   Dencrypt the encrypted string of characters to a valid set of credentials
        // ------------------------------------------------------------------------------------------------------------------------
        public static NetworkCredential DecryptCredentials(string encryptedCredentials)
        {
            string decAccount = "", decPassword = "";
            string[] Credentials = new string[2];
            // --------------------------------------------------------------------------------------------------------------------
            //   Get the encryption token and use the decryption method for that type
            // --------------------------------------------------------------------------------------------------------------------
            string[] ecParts = encryptedCredentials.Split(':');
            // --------------------------------------------------------------------------------------------------------------------
            //   For now V4 only!
            // --------------------------------------------------------------------------------------------------------------------
            if (ecParts[0] != "enc-V4-aes") { Logger.Error("Unsupported encryption method " + ecParts[0]); }
            // --------------------------------------------------------------------------------------------------------------------
            //   Reverse the work of EncryptCredentials, first get the seedBase
            // --------------------------------------------------------------------------------------------------------------------
            int ecpIndex = ecParts[1].Length / 2;
            byte[] accBase = new byte[ecpIndex]; byte[] pwdBase = new byte[ecpIndex];
            accBase = HexStringToByteArray(ecParts[1].Substring(0,ecpIndex));
            pwdBase = HexStringToByteArray(ecParts[1].Substring(ecpIndex));
            // --------------------------------------------------------------------------------------------------------------------
            //   Get lengths and offsets from the seedBase
            // --------------------------------------------------------------------------------------------------------------------
            int accOffset = accBase[0]; int accLength = accBase[1];
            int pwdOffset = pwdBase[0]; int pwdLength = pwdBase[1];
            // --------------------------------------------------------------------------------------------------------------------
            //   Get hashed password & account from the seedBase
            // --------------------------------------------------------------------------------------------------------------------
            string hashedAccount = Encoding.ASCII.GetString(accBase, accOffset, accLength);
            for (int i = 0; i < accLength; i++) { decAccount += (char)Convert.ToInt16(hashedAccount.Substring(i, 2), 16); i++; }
            string hashedPassword = Encoding.UTF8.GetString(pwdBase, pwdOffset, pwdLength);
            for (int i = 0; i < pwdLength; i++) { decPassword += (char)Convert.ToInt16(hashedPassword.Substring(i, 2), 16); i++; }
            // --------------------------------------------------------------------------------------------------------------------
            //   Use the Rotate13 method to 'unhash' the account & password and return the credentials
            // --------------------------------------------------------------------------------------------------------------------
            Credentials[0] = Rotate13(decAccount);
            Credentials[1] = Rotate13(decPassword);
            //return Credentials;
            return new NetworkCredential(Credentials[0], Credentials[1]);
        }

        // ========================================================================================================================
        //  ReplaceXMLCredentials - In an XML file - replace the account/password combination with an encrypted credential string
        // ------------------------------------------------------------------------------------------------------------------------
        public static void ReplaceXMLCredentials(string File, string searchNode, string Account, string Password, string Credential)
        {
            /// <summary>
            /// Replace the Account & Password attributes in an xml file with 1 encrypted credential attribute
            /// The value of this credential attribute can be decrypted using the DecryptCredentials function.
            /// </summary>
            /// <param name="xmlFile">The xml file to scan for the specufied attributes</param>
            /// <param name="searchNode">The node in the xml file which contains the attributes</param>
            /// <param name="Account">The name of the Account attribute</param>
            /// <param name="Password">The name of the Password attribute</param>
            /// <param name="Credential">The name of the Credential attribute, which will replace the Account & Password attributes</param>
            // --------------------------------------------------------------------------------------------------------------------
            //   Check the existance of the xml file and if found read/return its content  Does the file exist? NO? Quit!
            // --------------------------------------------------------------------------------------------------------------------
            FileInfo xmlFile = new FileInfo(ReplaceKeyword(File));
            if (!xmlFile.Exists) { Logger.Error("Config file " + xmlFile.FullName + " not found, terminating now..."); }
            // --------------------------------------------------------------------------------------------------------------------
            //  Read the file content and return it as an XML document
            // --------------------------------------------------------------------------------------------------------------------
            Logger.Debug("Replacing credentials in " + xmlFile.Name);
            XmlDocument xmlConfig = new XmlDocument();
            xmlConfig.Load(xmlFile.FullName);
            string xmlTransNode = "/*[translate(local-name(),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='{0}']";
            // --------------------------------------------------------------------------------------------------------------------
            //   The xml file is loaded, now search for the specified credential node
            // --------------------------------------------------------------------------------------------------------------------
            string credQuery = "";
            foreach (string node in searchNode.Split('.')) { credQuery += String.Format(xmlTransNode, node); }
            XmlNodeList credNodes = xmlConfig.SelectNodes(credQuery);
            if (credNodes.Count == 0) { Logger.Debug("No credentials to replace in " + xmlFile.Name); return; }
            // --------------------------------------------------------------------------------------------------------------------
            //   Now search each node found for the specified Account & Password attributes.
            //   When found, replace them with one encrypted credential attribute.
            // --------------------------------------------------------------------------------------------------------------------
            foreach (XmlElement credNode in credNodes)
            {
                string credAcc = credNode.GetAttribute(Account);
                if (credAcc.Length == 0) { Logger.Debug("No credentials to replace in " + xmlFile.Name); return; }
                string credPwd = credNode.GetAttribute(Password);
                if (credPwd.Length == 0) { Logger.Debug("No credentials to replace in " + xmlFile.Name); return; }
                string credString = EncryptCredentials(credAcc, credPwd);
                credNode.RemoveAttribute(Account);
                credNode.RemoveAttribute(Password);
                credNode.SetAttribute(Credential, credString);
            }
            // --------------------------------------------------------------------------------------------------------------------
            //   All attributes in all nodes found have been replaced, save the file and exit.
            // --------------------------------------------------------------------------------------------------------------------
            Logger.Debug("Saving update xml file " + xmlFile.Name);
            xmlConfig.Save(xmlFile.FullName);
        }

    }
    // ----------------------------------------------------------------------------------------------------------------------------
    // End of partial class app
    // ----------------------------------------------------------------------------------------------------------------------------
}
// ================================================================================================================================
//    EOF, Sayonara!
// ================================================================================================================================