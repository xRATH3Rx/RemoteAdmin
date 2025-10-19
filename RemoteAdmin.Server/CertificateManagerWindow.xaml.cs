using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RemoteAdmin.Server
{
    public partial class CertificateManagerWindow : Window
    {
        private const string CertificatePath = "Certificates";

        public CertificateManagerWindow()
        {
            InitializeComponent();
            CheckCertificates();
        }

        private void chkShowPasswords_Changed(object sender, RoutedEventArgs e)
        {
            bool showPasswords = chkShowPasswords.IsChecked == true;

            if (showPasswords)
            {
                // Copy passwords to visible textboxes
                txtCAPasswordVisible.Text = txtCAPassword.Password;
                txtServerPasswordVisible.Text = txtServerPassword.Password;
                txtClientPasswordVisible.Text = txtClientPassword.Password;

                // Hide PasswordBoxes, show TextBoxes
                txtCAPassword.Visibility = Visibility.Collapsed;
                txtServerPassword.Visibility = Visibility.Collapsed;
                txtClientPassword.Visibility = Visibility.Collapsed;

                txtCAPasswordVisible.Visibility = Visibility.Visible;
                txtServerPasswordVisible.Visibility = Visibility.Visible;
                txtClientPasswordVisible.Visibility = Visibility.Visible;
            }
            else
            {
                // Copy back to PasswordBoxes
                txtCAPassword.Password = txtCAPasswordVisible.Text;
                txtServerPassword.Password = txtServerPasswordVisible.Text;
                txtClientPassword.Password = txtClientPasswordVisible.Text;

                // Show PasswordBoxes, hide TextBoxes
                txtCAPassword.Visibility = Visibility.Visible;
                txtServerPassword.Visibility = Visibility.Visible;
                txtClientPassword.Visibility = Visibility.Visible;

                txtCAPasswordVisible.Visibility = Visibility.Collapsed;
                txtServerPasswordVisible.Visibility = Visibility.Collapsed;
                txtClientPasswordVisible.Visibility = Visibility.Collapsed;
            }
        }


        private void CheckCertificates()
        {
            // Try default passwords first, then custom
            CheckCertificate("ca.pfx", GetPassword(txtCAPassword, txtCAPasswordVisible),
                txtCAStatus, indicatorCA, btnViewCA);
            CheckCertificate("server.pfx", GetPassword(txtServerPassword, txtServerPasswordVisible),
                txtServerStatus, indicatorServer, btnViewServer);
            CheckCertificate("client.pfx", GetPassword(txtClientPassword, txtClientPasswordVisible),
                txtClientStatus, indicatorClient, btnViewClient);
        }

        private string GetPassword(PasswordBox passwordBox, TextBox textBox)
        {
            if (chkShowPasswords?.IsChecked == true)
                return textBox.Text;
            return string.IsNullOrEmpty(passwordBox.Password) ? "RemoteAdmin" + passwordBox.Name.Replace("txt", "").Replace("Password", "") : passwordBox.Password;
        }


        private void CheckCertificate(string filename, string password,
            System.Windows.Controls.TextBlock statusText,
            System.Windows.Shapes.Ellipse indicator,
            System.Windows.Controls.Button viewButton)
        {
            string path = Path.Combine(CertificatePath, filename);

            try
            {
                if (File.Exists(path))
                {
                    var cert = new X509Certificate2(path, password, X509KeyStorageFlags.Exportable);

                    // Check if expired
                    if (cert.NotAfter < DateTime.Now)
                    {
                        statusText.Text = $"Expired on {cert.NotAfter:yyyy-MM-dd}";
                        indicator.Fill = (SolidColorBrush)FindResource("DangerRed");
                        viewButton.IsEnabled = true;
                    }
                    // Check if expiring soon (30 days)
                    else if (cert.NotAfter < DateTime.Now.AddDays(30))
                    {
                        statusText.Text = $"Expires soon: {cert.NotAfter:yyyy-MM-dd}";
                        indicator.Fill = (SolidColorBrush)FindResource("WarningOrange");
                        viewButton.IsEnabled = true;
                    }
                    else
                    {
                        statusText.Text = $"Valid until {cert.NotAfter:yyyy-MM-dd}";
                        indicator.Fill = (SolidColorBrush)FindResource("SuccessGreen");
                        viewButton.IsEnabled = true;
                    }
                }
                else
                {
                    statusText.Text = "Not Found";
                    indicator.Fill = (SolidColorBrush)FindResource("DangerRed");
                    viewButton.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"Error: {ex.Message}";
                indicator.Fill = (SolidColorBrush)FindResource("DangerRed");
                viewButton.IsEnabled = false;
            }
        }


        private void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            string orgName = txtOrgName.Text.Trim();
            if (string.IsNullOrWhiteSpace(orgName))
            {
                MessageBox.Show("Organization name cannot be empty!", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get passwords
            string caPassword = chkShowPasswords.IsChecked == true ?
                txtCAPasswordVisible.Text : txtCAPassword.Password;
            string serverPassword = chkShowPasswords.IsChecked == true ?
                txtServerPasswordVisible.Text : txtServerPassword.Password;
            string clientPassword = chkShowPasswords.IsChecked == true ?
                txtClientPasswordVisible.Text : txtClientPassword.Password;

            // Validate passwords
            if (string.IsNullOrWhiteSpace(caPassword) || caPassword.Length < 8)
            {
                MessageBox.Show("CA password must be at least 8 characters!", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(serverPassword) || serverPassword.Length < 8)
            {
                MessageBox.Show("Server password must be at least 8 characters!", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(clientPassword) || clientPassword.Length < 8)
            {
                MessageBox.Show("Client password must be at least 8 characters!", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate validity periods
            if (!int.TryParse(txtCAYears.Text, out int caYears) || caYears < 1 || caYears > 30)
            {
                MessageBox.Show("CA validity must be between 1 and 30 years!", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(txtServerYears.Text, out int serverYears) || serverYears < 1 || serverYears > 10)
            {
                MessageBox.Show("Server validity must be between 1 and 10 years!", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(txtClientYears.Text, out int clientYears) || clientYears < 1 || clientYears > 10)
            {
                MessageBox.Show("Client validity must be between 1 and 10 years!", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Confirm if certificates already exist
            if (Directory.Exists(CertificatePath) && Directory.GetFiles(CertificatePath).Length > 0)
            {
                var result = MessageBox.Show(
                    "Existing certificates found. Generating new certificates will replace them.\n\n" +
                    "All clients using the old certificates will stop working and need to be rebuilt.\n\n" +
                    "⚠️ IMPORTANT: Write down your passwords! You'll need them later.\n\n" +
                    "Are you sure you want to continue?",
                    "Confirm Certificate Generation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            try
            {
                btnGenerate.IsEnabled = false;
                btnGenerate.Content = "⏳ Generating...";

                txtOutput.Text = "";
                AppendLog("Starting certificate generation...");
                AppendLog($"Organization: {orgName}");
                AppendLog($"CA Validity: {caYears} years");
                AppendLog($"Server Validity: {serverYears} years");
                AppendLog($"Client Validity: {clientYears} years");

                // Create directory
                Directory.CreateDirectory(CertificatePath);
                AppendLog($"✓ Created directory: {CertificatePath}");

                // Generate CA
                AppendLog("\n[1/3] Generating Certificate Authority (CA)...");
                var ca = GenerateCA(orgName, caYears);
                SaveCertificate(ca, Path.Combine(CertificatePath, "ca.pfx"), caPassword);
                SaveCertificatePublic(ca, Path.Combine(CertificatePath, "ca.crt"));
                AppendLog($"✓ CA Certificate: {ca.Thumbprint}");
                AppendLog($"  Valid: {ca.NotBefore:yyyy-MM-dd} to {ca.NotAfter:yyyy-MM-dd}");
                AppendLog($"  Password: {new string('•', caPassword.Length)}");

                // Generate Server Certificate
                AppendLog("\n[2/3] Generating Server Certificate...");
                var serverCert = GenerateServerCertificate(ca, orgName, serverYears);
                SaveCertificate(serverCert, Path.Combine(CertificatePath, "server.pfx"), serverPassword);
                AppendLog($"✓ Server Certificate: {serverCert.Thumbprint}");
                AppendLog($"  Valid: {serverCert.NotBefore:yyyy-MM-dd} to {serverCert.NotAfter:yyyy-MM-dd}");
                AppendLog($"  Password: {new string('•', serverPassword.Length)}");

                // Generate Client Certificate
                AppendLog("\n[3/3] Generating Client Certificate...");
                var clientCert = GenerateClientCertificate(ca, orgName, clientYears);
                SaveCertificate(clientCert, Path.Combine(CertificatePath, "client.pfx"), clientPassword);
                AppendLog($"✓ Client Certificate: {clientCert.Thumbprint}");
                AppendLog($"  Valid: {clientCert.NotBefore:yyyy-MM-dd} to {clientCert.NotAfter:yyyy-MM-dd}");
                AppendLog($"  Password: {new string('•', clientPassword.Length)}");

                // Save password reference
                string passwordFile = Path.Combine(CertificatePath, "README.txt");
                File.WriteAllText(passwordFile,
                    $"RemoteAdmin Certificate Information\n" +
                    $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"Organization: {orgName}\n\n" +
                    $"⚠️ IMPORTANT: Keep these passwords secure!\n\n" +
                    $"CA Password Length: {caPassword.Length} characters\n" +
                    $"Server Password Length: {serverPassword.Length} characters\n" +
                    $"Client Password Length: {clientPassword.Length} characters\n\n" +
                    $"Note: Actual passwords are NOT stored in this file for security.\n" +
                    $"You must remember or securely store them yourself.");

                AppendLog("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                AppendLog("✓ All certificates generated successfully!");
                AppendLog("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                AppendLog("\n⚠️ SAVE YOUR PASSWORDS NOW!");
                AppendLog("Without them, you cannot use the certificates!");
                AppendLog("\nNext Steps:");
                AppendLog("1. Restart the server to load new certificates");
                AppendLog("2. Rebuild ALL clients with the new client certificate");
                AppendLog("3. Old clients will no longer be able to connect");
                AppendLog($"\nCertificates location: {Path.GetFullPath(CertificatePath)}");

                CheckCertificates();

                MessageBox.Show(
                    "Certificates generated successfully!\n\n" +
                    "⚠️ IMPORTANT: Save your passwords securely!\n\n" +
                    "Remember to:\n" +
                    "1. Write down/store your passwords\n" +
                    "2. Restart the server\n" +
                    "3. Rebuild all clients",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"\n✗ Error: {ex.Message}");
                AppendLog($"\nStack trace:\n{ex.StackTrace}");

                MessageBox.Show(
                    $"Failed to generate certificates:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                btnGenerate.IsEnabled = true;
                btnGenerate.Content = "🔑 Generate Certificates";
            }
        }

        private void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(txtOutput.Text))
                    txtOutput.Text = message;
                else
                    txtOutput.Text += "\n" + message;

                // Auto-scroll to bottom
                var scrollViewer = FindScrollViewer(txtOutput);
                scrollViewer?.ScrollToEnd();
            });
        }

        private System.Windows.Controls.ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            if (obj is System.Windows.Controls.ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void btnViewCA_Click(object sender, RoutedEventArgs e)
        {
            ShowCertificateDetails("ca.pfx", "RemoteAdminCA");
        }

        private void btnViewServer_Click(object sender, RoutedEventArgs e)
        {
            ShowCertificateDetails("server.pfx", "RemoteAdminServer");
        }

        private void btnViewClient_Click(object sender, RoutedEventArgs e)
        {
            ShowCertificateDetails("client.pfx", "RemoteAdminClient");
        }

        private void ShowCertificateDetails(string filename, string password)
        {
            try
            {
                string path = Path.Combine(CertificatePath, filename);
                var cert = new X509Certificate2(path, password, X509KeyStorageFlags.Exportable);

                string details = $"Certificate Details\n" +
                    $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
                    $"Subject: {cert.Subject}\n" +
                    $"Issuer: {cert.Issuer}\n\n" +
                    $"Thumbprint: {cert.Thumbprint}\n" +
                    $"Serial Number: {cert.SerialNumber}\n\n" +
                    $"Valid From: {cert.NotBefore:yyyy-MM-dd HH:mm:ss} UTC\n" +
                    $"Valid To: {cert.NotAfter:yyyy-MM-dd HH:mm:ss} UTC\n\n" +
                    $"Key Algorithm: {cert.SignatureAlgorithm.FriendlyName}\n" +
                    $"Has Private Key: {cert.HasPrivateKey}\n\n" +
                    $"File: {path}";

                MessageBox.Show(details, "Certificate Details", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading certificate:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Certificate generation methods (same as CertificateGenerator but inline)
        // Update GenerateCA to accept parameters
        private X509Certificate2 GenerateCA(string orgName, int validityYears)
        {
            using var rsa = System.Security.Cryptography.RSA.Create(4096);

            var request = new CertificateRequest(
                $"CN={orgName} CA, O={orgName}, C=US",  // Use orgName parameter
                rsa,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, true, 2, true));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var ca = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(validityYears));  // Use validityYears parameter

            return new X509Certificate2(ca.Export(X509ContentType.Pfx), (string)null,
                X509KeyStorageFlags.Exportable);
        }

        // Update GenerateServerCertificate to accept parameters
        private X509Certificate2 GenerateServerCertificate(X509Certificate2 ca, string orgName, int validityYears)
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);

            var request = new CertificateRequest(
                $"CN={orgName} Server, O={orgName}, C=US",  // Use orgName parameter
                rsa,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new System.Security.Cryptography.OidCollection {
                new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1")
                    }, false));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Sign with CA
            var serverCert = request.Create(
                ca.SubjectName,
                X509SignatureGenerator.CreateForRSA(ca.GetRSAPrivateKey(), System.Security.Cryptography.RSASignaturePadding.Pkcs1),
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(validityYears),  // Use validityYears parameter
                GenerateSerialNumber());

            // Combine the certificate with its private key
            return serverCert.CopyWithPrivateKey(rsa);
        }

        // Update GenerateClientCertificate to accept parameters
        private X509Certificate2 GenerateClientCertificate(X509Certificate2 ca, string orgName, int validityYears)
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);

            var request = new CertificateRequest(
                $"CN={orgName} Client, O={orgName}, C=US",  // Use orgName parameter
                rsa,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new System.Security.Cryptography.OidCollection {
                new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.2")
                    }, false));

            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));

            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            // Sign with CA
            var clientCert = request.Create(
                ca.SubjectName,
                X509SignatureGenerator.CreateForRSA(ca.GetRSAPrivateKey(), System.Security.Cryptography.RSASignaturePadding.Pkcs1),
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(validityYears),  // Use validityYears parameter
                GenerateSerialNumber());

            // Combine the certificate with its private key
            return clientCert.CopyWithPrivateKey(rsa);
        }

        private byte[] GenerateSerialNumber()
        {
            var serialNumber = new byte[16];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(serialNumber);
            }
            serialNumber[0] &= 0x7F;
            return serialNumber;
        }

        private void SaveCertificate(X509Certificate2 cert, string path, string password)
        {
            var pfxData = cert.Export(X509ContentType.Pfx, password);
            File.WriteAllBytes(path, pfxData);
        }

        private void SaveCertificatePublic(X509Certificate2 cert, string path)
        {
            var certData = cert.Export(X509ContentType.Cert);
            File.WriteAllBytes(path, certData);
        }
    }
}