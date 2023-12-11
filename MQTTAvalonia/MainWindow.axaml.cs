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
			string topic = SubscribeTopicTextBox.Text;

			// Abonnieren des angegebenen Topics
			client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });

			// Möglicherweise hier auch das Empfangen von Nachrichten behandeln
			// client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
		}

		#region Methods

		private void Connect(object? sender, RoutedEventArgs e)
		{
			string brokerUrl = BrokerUrlTextBox.Text;
			client = new MqttClient(brokerUrl);
			client.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;

			// Verbindung zum Broker herstellen
			string clientId = Guid.NewGuid().ToString();
    
			try
			{
				client.Connect(clientId);
				ConnectionStatusTextBox.Text = "Connected";
			}
			catch (Exception ex)
			{
				ConnectionStatusTextBox.Text = "Connection failed";
				Console.WriteLine("Connection failed: " + ex.Message);
				// Hier kannst du entsprechend auf den Verbindungsfehler reagieren
			}
		}


		
		private void PublishMessage(object sender, RoutedEventArgs e)
		{
			string topic = TopicTextBox.Text;
			string message = MessageTextBox.Text;

			// Aktualisieren der empfangenen Nachrichten, indem die neue Nachricht hinzugefügt wird
			UpdateReceivedMessages($"Topic: {topic}, Nachricht: {message}");

			// Hier kannst du den Publish-Vorgang fortsetzen, wenn gewünscht
			// client.Publish(topic, System.Text.Encoding.UTF8.GetBytes(message));
			// client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
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
				ConnectionStatusTextBox.Text = "Disconnected";
			}
			else
			{
				ConnectionStatusTextBox.Text = "Client is either null or not connected.";
				// Handle the case where the client is null or not connected
				// This could involve displaying a message or logging an error
				Console.WriteLine("Client is either null or not connected.");
			}

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
			string lastTopicID = GetLastTopicIDFromDatabase(); // Annahme: Methode, um die ID des letzten Topics abzurufen
			string lastTopic = GetTopic(lastTopicID); // Verwendung der GetTopic-Methode, um den Namen des Topics abzurufen
			string lastMessage = GetMessage(lastTopic); // Verwendung der GetMessage-Methode, um die letzte Nachricht für das Topic abzurufen

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