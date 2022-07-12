using NLog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace MIC_AMZ_API.App_Code
{
    public class MailHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly ConfigureSMTP configureSMTP;

        public MailHelper(ConfigureSMTP configureSMTP)
        {
            if (configureSMTP != null)
            {
                this.configureSMTP = configureSMTP;
            }
        }

        public void SendEmail(string to, string title, string subject, string body, List<Attachment> attachments = null)
        {
            try
            {
                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(configureSMTP.MailAddress, title);
                    mailMessage.To.Add(to);
                    mailMessage.SubjectEncoding = Encoding.UTF8;
                    mailMessage.Subject = subject;
                    mailMessage.BodyEncoding = Encoding.UTF8;
                    mailMessage.Body = body;
                    mailMessage.IsBodyHtml = true;
                    mailMessage.Priority = MailPriority.Normal;
                    if (attachments != null)
                    {
                        foreach (var item in attachments)
                        {
                            mailMessage.Attachments.Add(item);
                        }
                    }
                    using (var smtpClien = new SmtpClient())
                    {
                        smtpClien.Host = configureSMTP.Host;
                        smtpClien.Port = configureSMTP.Port;
                        smtpClien.DeliveryMethod = SmtpDeliveryMethod.Network;
                        smtpClien.UseDefaultCredentials = false;
                        smtpClien.Credentials = new NetworkCredential(configureSMTP.Credentials.UserName, configureSMTP.Credentials.Password);
                        smtpClien.Send(mailMessage);
                    }
                }
            }
            catch (Exception e) {
                logger.Error(e);
            }
        }

        public async Task SendEmailAsync(string to, string title, string subject, string body, List<Attachment> attachments = null)
        {
            await Task.Run(() =>
            {
                SendEmail(to, title, subject, body, attachments);
            });
        }
    }
}