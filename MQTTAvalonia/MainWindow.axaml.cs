using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Microsoft.Data.Sqlite;

namespace MQTTAvalonia
{
    public partial class MainWindow : Window
    {
        // Broker URI und Port
        static string brokerUri = "broker.hivemq.com";
        // MQTT-Client erstellen
        MqttClient client = new MqttClient(brokerUri);
        private string receivedMessage;
        
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
        public string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) , "MQTT_Broker");
        public string DB_path = "MQTT_DB.db";

        #endregion // Properties

       #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            create_DB_ifnotexists();
            BindingTest = "Test123";
        }
        #endregion
        
        #region Methods
        private void Connect(object? sender, RoutedEventArgs e)
        {
            // Optional: Event-Handler für eingehende Nachrichten
            string brokerUrl = BrokerUrlTextBox.Text;
            client = new MqttClient(brokerUrl);
            client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;

            // Verbindung zum Broker herstellen
            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);
        }
        
        private void Disconnect(object? sender, RoutedEventArgs e)
        {
	        client.Disconnect();
        }
        
        private void PublishMessage(object sender, RoutedEventArgs e)
        {
	        string topic = TopicTextBox.Text;
	        string message = MessageTextBox.Text;

	        client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message));
	        client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
        }
        private void DeleteReceivedMessages(object sender, RoutedEventArgs e)
        {
	        ReceivedMessagesTextBox.Text = "";
        }
        
        
        private void Submit(object sender, RoutedEventArgs e)
        {
            // Nachricht veröffentlichen
            string topic = "test/topic";
            string message = "Hello, MQTT!";
            client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message));

            // Auf Nachrichten in einem bestimmten Thema hören
            client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
        }

        private void Receive(object sender, RoutedEventArgs e)
        {
            // Angekommene Nachricht anzeigen
            ReceivedMessagesTextBox.Text = receivedMessage;
        }

        private void DisconnectClicked(object? sender, RoutedEventArgs e)
        {
            // Verbindung trennen
            client.Disconnect();
        }

        private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            // Verarbeitung der eingehenden Nachricht
            string receivedMessage = System.Text.Encoding.UTF8.GetString(e.Message);
            
            // Operation auf dem UI-Thread ausführen
            Dispatcher.UIThread.InvokeAsync(() =>
            {
	            // Update der empfangenen Nachrichten in der großen Textbox
	            UpdateReceivedMessages(receivedMessage);
            });
        }
        
        private void UpdateReceivedMessages(string message)
        {
	        ReceivedMessagesTextBox.Text += $"{message}\n";
        }
        
        
        
        #endregion
        
        #region Sqllite
        
        public void create_DB_ifnotexists()
        		{
        			//Ordner erstellen, wenn nicht existent
        			if (Directory.Exists(appdata) == false)
        			{
        				Directory.CreateDirectory(appdata);
        			}
        
        			//DB erstellen, wenn nicht existent
        			if (!File.Exists(Path.Combine(appdata , DB_path)))
        			{
        				File.Create(Path.Combine(appdata , DB_path));
        				//Application.Restart();
        				//Restart damit DB erkannt wird
        			}
        
        			var test = Path.Combine(appdata, DB_path);
        			using (var connect = new SqliteConnection("Data Source=" + Path.Combine(appdata , DB_path)))
        			{
        				connect.Open();
        
        				//SQL Commands zum erstellen der Tabellen
        				var createT1 = "CREATE TABLE IF NOT EXISTS STOPIC (TID INTEGER, TNAME TEXT, MID INTEGER, PRIMARY KEY(TID))";
        				var createT2 = "CREATE TABLE IF NOT EXISTS SMSG (MID INTEGER, MTEXT TEXT, PRIMARY KEY(MID))";
        
        				//ausführen des Commands
        				var cmd = new SqliteCommand(createT1, connect);
        				cmd.ExecuteNonQuery();
        
        				//ausführen des Commands
        				cmd = new SqliteCommand(createT2, connect);
        				cmd.ExecuteNonQuery();
        			}
        		}
        
        public string get_Topic(string id)
        {
	        //get Topic from id
	        using (SqliteConnection con = new SqliteConnection("Data Source=" + appdata + DB_path))
	        {
		        string Select_Topic = "SELECT TNAME FROM STOPIC WHERE TID = " + id;

		        SqliteCommand cmd = new SqliteCommand(Select_Topic, con);

		        string Result = Convert.ToString(cmd.ExecuteScalar());

		        return (Result);
	        }
        }
        
        private string get_MSG(string Topic)
        {
	        //get MSG from Topic
	        using (SqliteConnection connect = new SqliteConnection("Data Source=" + appdata + DB_path))
	        {
		        string Select_MSG = "SELECT MTEXT FROM SMSG WHERE MID = (SELECT MID FROM STOPIC WHERE TNAME =" + Topic + ")";

		        SqliteCommand cmd = new SqliteCommand(Select_MSG, connect);

		        string Result = Convert.ToString(cmd.ExecuteScalar());

		        return (Result);
	        }
        }
        
        #endregion
        
    }
}