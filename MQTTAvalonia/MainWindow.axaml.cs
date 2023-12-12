using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Microsoft.Data.Sqlite;
using System.Xml.Schema;
using Avalonia.Media;
using Tmds.DBus.Protocol;

namespace MQTTAvalonia
{
    public partial class MainWindow : Window
    {
        // Deklariere die XAML-Elemente, um sie in der C#-Klasse zu verwenden


        // Broker URI und Port
        static string brokerUri = "broker.hivemq.com";

        // MQTT-Client erstellen
        MqttClient client = new MqttClient(brokerUri);
        private string receivedMessage;

        List<string> m_topicStringList = new List<string>();
        List<string> m_subscriptionStringList = new List<string>();

        #region Properties

        public string? BindingTest { get; set; }
        public MqttClient? Client { get; set; }
        public string? BrokerUri { get; set; }
        public bool UseAuth { get; set; }
        public string? AuthUser { get; set; }
        public string? AuthPass { get; set; }
        public int? ConnectionStatus { get; set; }
        public List<string> AvailableTopics { get; set; } = new List<string>();
        public List<string> SelectedTopics { get; set; } = new List<string>();
        public string? Topic { get; set; }
        public string? Message { get; set; }



        public string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MQTT_Broker");

        public string DB_path = "MQTT_DB.db";

        #endregion // Properties

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            CreateDBIfNotExists();
            BindingTest = "Test123";
        }

        #endregion

        private void SubscribeToTopic(object sender, RoutedEventArgs e)
        {
            if (topicListBox.SelectedIndex != -1)
            {
                string topic = m_topicStringList[topicListBox.SelectedIndex];

                // Abonnieren des angegebenen Topics
                client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });

                // Möglicherweise hier auch das Empfangen von Nachrichten behandeln
                client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
                AddTopicToSubscriptionList(topic);
            }

        }

        #region Methods

        private void Connect(object? sender, RoutedEventArgs e)
        {
            string brokerUrl = BrokerUrlTextBox.Text;
            if (string.IsNullOrEmpty(brokerUrl))
            {
                connectionStatusTextBlock.Text = "Connection failed";
                connectionStatusRectangle.Fill = Brushes.Red;
                return;
            }

            try
            {
                client = new MqttClient(brokerUrl);
                client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            }
            catch (Exception ex)
            {
                connectionStatusTextBlock.Text = "Connection failed";
                connectionStatusRectangle.Fill = Brushes.Red;
                Console.WriteLine("Connection failed: " + ex.Message);
                return;
            }

            // Verbindung zum Broker herstellen
            string clientId = Guid.NewGuid().ToString();

            try
            {
                client.Connect(clientId);
                connectionStatusTextBlock.Text = "Connected";
                connectionStatusRectangle.Fill = Brushes.Green;
            }
            catch (Exception ex)
            {
                connectionStatusTextBlock.Text = "Connection failed";
                connectionStatusRectangle.Fill = Brushes.Red;
                Console.WriteLine("Connection failed: " + ex.Message);
            }
        }



        private void PublishMessage(object sender, RoutedEventArgs e)
        {
            if (topicListBox.SelectedIndex == -1)
            {
                MessageTextBox.Text = "select a topic";
                return;
            }

            string topic = m_topicStringList[topicListBox.SelectedIndex];
            string message = MessageTextBox.Text;

            AddTopicToSubscriptionList(topic);
            // Aktualisieren der empfangenen Nachrichten, indem die neue Nachricht hinzugefügt wird
            UpdateReceivedMessages($"Topic: {topic}, Nachricht: {message}");

            // Hier kannst du den Publish-Vorgang fortsetzen, wenn gewünscht
            client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message));
        }

        private void DeleteReceivedMessages(object sender, RoutedEventArgs e)
        {
            ReceivedMessagesTextBox.Text = "";
        }


        private void DisconnectClicked(object? sender, RoutedEventArgs e)
        {
            if (client != null && client.IsConnected)
            {
                client.Disconnect();
                connectionStatusTextBlock.Text = "Disconnected";
                connectionStatusRectangle.Fill = Brushes.Red;
            }
            else
            {
                //ConnectionStatusTextBox.Text = "Client is either null or not connected.";
                // Handle the case where the client is null or not connected
                // This could involve displaying a message or logging an error
                Console.WriteLine("Client is either null or not connected.");
            }

        }

        private void AddTopicButtonClicked(object? sender, RoutedEventArgs e)
        {
            string topicName = enterTopicName.Text;
            if (!string.IsNullOrEmpty(topicName) && client.IsConnected)
            {
                client.Publish(topicName, System.Text.Encoding.UTF8.GetBytes("Topic created"));
                AddTopicToList(topicName);
            }
        }

        private void AddTopicToList(string topicName)
        {
            m_topicStringList.Add(topicName);
            ListBox listBox = this.FindControl<ListBox>("topicListBox");
            listBox.ItemsSource = m_topicStringList;
        }

        private void AddTopicToSubscriptionList(string topicName)
        {
            m_subscriptionStringList.Add(topicName);
            ListBox listBox = this.FindControl<ListBox>("subscriptionListBox");
            listBox.ItemsSource = m_subscriptionStringList;
        }

        // Aktualisierte Methode zum Behandeln von empfangenen Nachrichten für abonnierte Topics
        private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string receivedMessage = System.Text.Encoding.UTF8.GetString(e.Message);

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Überprüfen, ob die empfangene Nachricht zu einem abonnierten Topic gehört
                string subscribedTopic = SelectedTopics.FirstOrDefault(t => t.Equals(e.Topic));
                if (subscribedTopic != null)
                {
                    // Anzeigen der empfangenen Nachricht für das abonnierte Topic
                    UpdateReceivedMessages($"Topic: {e.Topic}, Nachricht: {receivedMessage}");
                }
            });
        }

        private void UpdateReceivedMessages(string message)
        {
            ReceivedMessagesTextBox.Text += $"{message}\n";
        }

        private void ShowLastMessage(object sender, RoutedEventArgs e)
        {
            string
                lastTopicID = GetLastTopicIDFromDatabase(); // Annahme: Methode, um die ID des letzten Topics abzurufen
            string
                lastTopic = GetTopic(lastTopicID); // Verwendung der GetTopic-Methode, um den Namen des Topics abzurufen
            string
                lastMessage =
                    GetMessage(lastTopic); // Verwendung der GetMessage-Methode, um die letzte Nachricht für das Topic abzurufen

            // Setzen der letzten Nachricht in die MessageTextBox
            MessageTextBox.Text = lastMessage;
        }





        #endregion

        #region Sqllite

        public void CreateDBIfNotExists()
        {
            if (!Directory.Exists(appdata))
            {
                Directory.CreateDirectory(appdata);
            }

            if (!File.Exists(Path.Combine(appdata, DB_path)))
            {
                File.Create(Path.Combine(appdata, DB_path)).Close();
            }

            using (var connection = new SqliteConnection("Data Source=" + Path.Combine(appdata, DB_path)))
            {
                connection.Open();

                var createT1 =
                    "CREATE TABLE IF NOT EXISTS STOPIC (TID INTEGER, TNAME TEXT, MID INTEGER, PRIMARY KEY(TID))";
                var createT2 = "CREATE TABLE IF NOT EXISTS SMSG (MID INTEGER, MTEXT TEXT, PRIMARY KEY(MID))";

                var cmd = new SqliteCommand(createT1, connection);
                cmd.ExecuteNonQuery();

                cmd = new SqliteCommand(createT2, connection);
                cmd.ExecuteNonQuery();
            }
        }

        private string GetLastTopicIDFromDatabase()
        {
            using (SqliteConnection con = new SqliteConnection("Data Source=" + appdata + DB_path))
            {
                string selectLastTopicID = "SELECT MAX(TID) FROM STOPIC";

                using (SqliteCommand cmd = new SqliteCommand(selectLastTopicID, con))
                {
                    con.Open();
                    object result = cmd.ExecuteScalar();
                    return result?.ToString() ?? string.Empty;
                }
            }
        }



        public string GetTopic(string id)
        {
            using (SqliteConnection con = new SqliteConnection("Data Source=" + appdata + DB_path))
            {
                string selectTopic = "SELECT TNAME FROM STOPIC WHERE TID = " + id;

                using (SqliteCommand cmd = new SqliteCommand(selectTopic, con))
                {
                    object result = cmd.ExecuteScalar();
                    return result?.ToString() ?? string.Empty;
                }
            }
        }


        private string GetMessage(string topic)
        {
            using (SqliteConnection connect = new SqliteConnection("Data Source=" + appdata + DB_path))
            {
                string selectMsg = "SELECT MTEXT FROM SMSG WHERE MID = (SELECT MID FROM STOPIC WHERE TNAME = @Topic)";
                using (SqliteCommand cmd = new SqliteCommand(selectMsg, connect))
                {
                    cmd.Parameters.AddWithValue("@Topic", topic);
                    object result = cmd.ExecuteScalar();
                    return result?.ToString() ?? string.Empty;
                }
            }

            #endregion

        }
    }
}
