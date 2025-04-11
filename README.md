# Dynamics365MailgunPlugin

A Dynamics 365 CRM plugin that integrates with Mailgun for sending emails. This plugin triggers on the 'Send' event for emails, validates the sender as a Queue with outgoing mail enabled, gathers inline images and attachments, and sends the email via Mailgun.

## Features

- Triggers on the 'Send' event for emails
- Validates sender as a Queue with outgoing mail enabled
- Gathers inline images and attachments
- Sends emails via Mailgun
- Handles email addresses, attachments, and error management

## Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or Visual Studio Code
- Dynamics 365 SDK
- Mailgun account and API key

## Installation

1. Clone the repository
2. Build the project:
   ```bash
   dotnet build
   ```
3. Deploy the plugin to your Dynamics 365 instance

## Configuration

1. Configure your Mailgun API key in the plugin settings
2. Set up the Queue with outgoing mail enabled in Dynamics 365
3. Configure the plugin to trigger on the 'Send' event for emails

## Usage

1. Create an email in Dynamics 365
2. Add recipients, subject, and body
3. Add attachments if needed
4. Send the email
5. The plugin will trigger and send the email via Mailgun

## Contributing

Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

If you have any questions or issues, please open an issue in the repository. 