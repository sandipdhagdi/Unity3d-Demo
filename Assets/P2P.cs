using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using System;
using System.Text;

public class P2P : MonoBehaviour {

	//Server variables
	private int listenPort	= 5000; 	//server port

	//Client variable
	private int clientPort  = 5100; 	//client port
	private String server_ip ;				//catched server ip

	//paring variables
	private IPAddress group_address = IPAddress.Parse ("224.0.0.224");
	private UdpClient udpPairingClient  ;
	private IPEndPoint ipPairingEnd   ;
 


	//Toggle color
	private UdpClient udpSendData  ;
	private UdpClient udpListenData  ;

	private int sendPortData  = 5101; 	//client port
	private int listenPortData	= 5001; 	//server port

	private IPEndPoint sendEndPoint   ;
	private IPEndPoint listenEndPoint   ;

	private IPEndPoint servertoclientendpoint   ;



	UdpClient servertoclientudp ; 
	//UI variables
	//private IPEndPoint server_end  ;
	public Text heading;

	public Text serverState;
	public Text clientState;
	public Text connections;
	public Image bannerImage;

	int color = 1;
	bool shouldChangeColor = false;


	//Local functions

	void changeColor(){
		if (color == 1) {
			color = 0;
			bannerImage.color = Color.green;
		} else {
			color = 1;
			bannerImage.color = Color.cyan;
		}
	}


	void startListeningClient(){
		listenEndPoint = new IPEndPoint ( IPAddress.Parse(Network.player.ipAddress) , listenPortData);
		udpListenData = new UdpClient (listenEndPoint);
		try{
			udpListenData.BeginReceive (new AsyncCallback (receivedCommand), null);
		}
		catch (Exception e){
			Debug.Log (e.Message);
		} 
	}

	void startListeningServer(){
 
		Debug.Log ("\n*****startListeningServer");

		servertoclientendpoint = new IPEndPoint (IPAddress.Any, 6001);
		servertoclientudp = new UdpClient (servertoclientendpoint);
		servertoclientudp.JoinMulticastGroup (group_address);
		try{
 			servertoclientudp.BeginReceive (new AsyncCallback (receivedServerCommand), null);
		}
		catch (Exception e){
			Debug.Log (e.Message);
		}
	}

	//Outlets
	public void btnToggleColorClicked(){
	//	clientPort -> server -- working
		if (Network.peerType == NetworkPeerType.Client) {
			if (String.IsNullOrEmpty (server_ip))
				return;
			udpSendData = new UdpClient ();
			udpSendData.JoinMulticastGroup (group_address);
			var endPoint = new IPEndPoint (IPAddress.Parse (server_ip), listenPortData);
			var buffer = Encoding.ASCII.GetBytes ("tooglecolor");
			udpSendData.Send (buffer, buffer.Length, endPoint);

		}
		else {

			// multicast send setup
//			udpSendData = new UdpClient ();
//			udpSendData.JoinMulticastGroup (group_address);
//			sendEndPoint = new IPEndPoint (group_address, 6001);
//			var buffer = Encoding.ASCII.GetBytes ("tooglecolor");
//			udpPairingClient.Send (buffer, buffer.Length, sendEndPoint);
//			 

			var connections = Network.connections;
			if (connections.Length > 0) {
 				udpSendData = new UdpClient ();
 				udpSendData.JoinMulticastGroup (group_address);
 				foreach (var connection in connections){
 					var ip = connection.ipAddress;
					var endPoint = new IPEndPoint (IPAddress.Parse (ip), 6001);
 					var buffer = Encoding.ASCII.GetBytes ("tooglecolor");
 					udpSendData.Send (buffer, buffer.Length, endPoint);
 				}
 			}
 		}
	}

	public void   btnLogoutClicked(){
		Network.Disconnect(10);
	}

	public void   btnConnectClicked(){
		Debug.Log ("Button btnConnectClicked clicked");	
		StartGameClient ();
		startListeningServer ();
		heading.text = "Client";
	}

	public void btnStartServerClicked(){
		Debug.Log ("Button btnStartServerClicked clicked");		
		StartGameServer ();
		startListeningClient ();
		heading.text = "Server";
	}

	//Server functions

	void receivedServerCommand (IAsyncResult ar) {
		var receiveBytes = servertoclientudp.EndReceive (ar, ref servertoclientendpoint);
		string text = Encoding.UTF8.GetString(receiveBytes);
		shouldChangeColor = true;
		Debug.Log ("\nReceived Message ::" + text);
		if (servertoclientudp != null) {
			try{
				servertoclientudp.BeginReceive (new AsyncCallback (receivedServerCommand), null);
			}
			catch (Exception e){
				Debug.Log (e.Message);
			} 
		}
	}


	void receivedCommand (IAsyncResult ar)
	{
		var receiveBytes = udpListenData.EndReceive (ar, ref listenEndPoint);
		string text = Encoding.UTF8.GetString(receiveBytes);
		shouldChangeColor = true;
		Debug.Log ("\nReceived Message ::" + text);

		if (udpListenData != null) {
			try{
				udpListenData.BeginReceive (new AsyncCallback (receivedCommand), null);
			}
			catch (Exception e){
				Debug.Log (e.Message);
			} 
		}
	}

 

	void StartGameServer ()
	{
		// the Unity3d way to become a server
		var init_status = Network.InitializeServer (10, listenPort, false);
		Debug.Log ("status: " + init_status);

		StartCoroutine( StartBroadcast ());
	}

	void   StartGameClient ()
	{
		Debug.Log ("StartGameClient:: started");
		// multicast receive setup
		ipPairingEnd = new IPEndPoint (IPAddress.Any, clientPort);
		udpPairingClient = new UdpClient (ipPairingEnd);
		udpPairingClient.JoinMulticastGroup (group_address);
		try{
			// async callback for multicast
			udpPairingClient.BeginReceive (new AsyncCallback (ServerLookup), null);

		}
		catch (Exception e){
			Debug.Log (e.Message);
		}
		StartCoroutine(MakeConnection ());
	}

	IEnumerator MakeConnection ()
	{
		Debug.Log ("\nMakeConnection");
		// continues after we get server's address
		while (String.IsNullOrEmpty (server_ip)) {
			Debug.Log("\nserver_ip is empty");
			yield return new WaitForSeconds (1);
		}

		while (Network.peerType == NetworkPeerType.Disconnected)
		{
			Debug.Log ("connecting: " + server_ip +":"+ listenPort);

			// the Unity3d way to connect to a server
			NetworkConnectionError error ;
			error = Network.Connect (server_ip, listenPort);

			Debug.Log ("status: " + error);
			yield return new WaitForSeconds(1);
		}
	}



	/******* broadcast functions *******/
	void ServerLookup (IAsyncResult ar)	 
	{
 
		var receiveBytes = udpPairingClient.EndReceive (ar, ref ipPairingEnd);
		string text = Encoding.UTF8.GetString(receiveBytes);
		server_ip = ipPairingEnd.Address.ToString ();

		Debug.Log ("Server: " + server_ip);
		Debug.Log ("\nReceived Message ::" + text);
	}

	IEnumerator StartBroadcast ()
	{
		Debug.Log("StartBroadcast created");
		// multicast send setup
		udpPairingClient = new UdpClient ();
		udpPairingClient.JoinMulticastGroup (group_address);
		ipPairingEnd = new IPEndPoint (group_address, clientPort);

		// sends multicast
		while (true)
		{
			//Debug.Log ("\n Brodcasting server" );
			var buffer = Encoding.ASCII.GetBytes ("GameServer");
			udpPairingClient.Send (buffer, buffer.Length, ipPairingEnd);
			yield return new WaitForSeconds(1);
		}
	}

	void OnGUI(){

		if (shouldChangeColor == true) {
			this.changeColor ();
			this.shouldChangeColor = false;
		}

		connections.text = "Connections:: " + Network.connections.Length;
		if (Network.peerType == NetworkPeerType.Disconnected) {
			serverState.text = "Server State";
			clientState.text = "Client connected to ::";
		}
		else {
			if (Network.peerType == NetworkPeerType.Server){
					serverState.text = "Server started at ::" + Network.player.ipAddress;				
			}
			if (Network.peerType == NetworkPeerType.Client){
				if (!String.IsNullOrEmpty (server_ip)) {
					serverState.text = "Server started at ::" + server_ip;	
					clientState.text = "Client connected to ::" + server_ip ;
				}
			}
		}
	}
}
