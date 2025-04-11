using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Mailgun;

namespace Dynamics365MailgunPlugin
{
    public class MailgunEmailPlugin : IPlugin
    {
        private readonly string _mailgunApiKey;
        private readonly string _mailgunDomain;

        public MailgunEmailPlugin(string unsecureConfig, string secureConfig)
        {
            // Parse configuration
            if (!string.IsNullOrEmpty(secureConfig))
            {
                _mailgunApiKey = secureConfig;
            }
            else if (!string.IsNullOrEmpty(unsecureConfig))
            {
                _mailgunApiKey = unsecureConfig;
            }
            else
            {
                throw new InvalidPluginExecutionException("Mailgun API key not configured");
            }

            // Get domain from configuration
            _mailgunDomain = "your-domain.mailgun.org"; // Replace with your domain
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            // Get the tracing service
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Get the execution context
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Get the organization service
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                // Verify that the plugin is triggered on the Send message
                if (context.MessageName != "Send")
                {
                    return;
                }

                // Get the email entity
                Entity email = context.InputParameters["EmailId"] as Entity;
                if (email == null)
                {
                    throw new InvalidPluginExecutionException("Email entity not found");
                }

                // Verify that the email is from a queue
                EntityReference fromQueue = email.GetAttributeValue<EntityReference>("from");
                if (fromQueue == null || fromQueue.LogicalName != "queue")
                {
                    return; // Not from a queue, let CRM handle it
                }

                // Get the queue entity
                Entity queue = service.Retrieve("queue", fromQueue.Id, new ColumnSet("emailaddress", "outgoingemaildeliverymethod"));
                if (queue == null)
                {
                    throw new InvalidPluginExecutionException("Queue not found");
                }

                // Verify that the queue has outgoing email enabled
                int outgoingEmailDeliveryMethod = queue.GetAttributeValue<int>("outgoingemaildeliverymethod");
                if (outgoingEmailDeliveryMethod != 2) // 2 = Email Router
                {
                    return; // Not configured for outgoing email, let CRM handle it
                }

                // Get email details
                string subject = email.GetAttributeValue<string>("subject");
                string body = email.GetAttributeValue<string>("description");
                string fromAddress = queue.GetAttributeValue<string>("emailaddress");

                // Get recipients
                List<string> toRecipients = GetRecipients(service, email.Id, "to");
                List<string> ccRecipients = GetRecipients(service, email.Id, "cc");
                List<string> bccRecipients = GetRecipients(service, email.Id, "bcc");

                // Get attachments
                List<Attachment> attachments = GetAttachments(service, email.Id);

                // Send email via Mailgun
                SendEmailViaMailgun(subject, body, fromAddress, toRecipients, ccRecipients, bccRecipients, attachments);

                // Prevent CRM from sending the email
                context.SharedVariables["MailgunEmailSent"] = true;
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"An error occurred: {ex.Message}");
            }
        }

        private List<string> GetRecipients(IOrganizationService service, Guid emailId, string recipientType)
        {
            QueryExpression query = new QueryExpression("activityparty");
            query.ColumnSet = new ColumnSet("partyid");
            query.Criteria.AddCondition("activityid", ConditionOperator.Equal, emailId);
            query.Criteria.AddCondition("participationtypemask", ConditionOperator.Equal, recipientType);

            EntityCollection results = service.RetrieveMultiple(query);
            List<string> recipients = new List<string>();

            foreach (Entity party in results.Entities)
            {
                EntityReference partyId = party.GetAttributeValue<EntityReference>("partyid");
                if (partyId != null)
                {
                    Entity contact = service.Retrieve(partyId.LogicalName, partyId.Id, new ColumnSet("emailaddress1"));
                    string email = contact.GetAttributeValue<string>("emailaddress1");
                    if (!string.IsNullOrEmpty(email))
                    {
                        recipients.Add(email);
                    }
                }
            }

            return recipients;
        }

        private List<Attachment> GetAttachments(IOrganizationService service, Guid emailId)
        {
            QueryExpression query = new QueryExpression("activitymimeattachment");
            query.ColumnSet = new ColumnSet("filename", "mimetype", "body");
            query.Criteria.AddCondition("objectid", ConditionOperator.Equal, emailId);

            EntityCollection results = service.RetrieveMultiple(query);
            List<Attachment> attachments = new List<Attachment>();

            foreach (Entity attachment in results.Entities)
            {
                attachments.Add(new Attachment
                {
                    FileName = attachment.GetAttributeValue<string>("filename"),
                    MimeType = attachment.GetAttributeValue<string>("mimetype"),
                    Content = attachment.GetAttributeValue<string>("body")
                });
            }

            return attachments;
        }

        private void SendEmailViaMailgun(string subject, string body, string fromAddress, List<string> toRecipients, List<string> ccRecipients, List<string> bccRecipients, List<Attachment> attachments)
        {
            var mailgun = new MailgunClient(_mailgunDomain, _mailgunApiKey);

            var message = new MailgunMessage
            {
                From = fromAddress,
                Subject = subject,
                Html = body,
                To = toRecipients,
                Cc = ccRecipients,
                Bcc = bccRecipients
            };

            foreach (var attachment in attachments)
            {
                message.Attachments.Add(new MailgunAttachment
                {
                    Filename = attachment.FileName,
                    ContentType = attachment.MimeType,
                    Data = Convert.FromBase64String(attachment.Content)
                });
            }

            mailgun.SendMessageAsync(message).Wait();
        }
    }

    public class Attachment
    {
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public string Content { get; set; }
    }
} 