MessageFramework应用TCP工作流/数据流

                                                                                       
                                                                                                   
                                                                              
					                                                                                    
		                            
		   		                                                                                              
					                                                      
                                                                                                                 
		                                                                              
                                                                                                                    
Client.Start                              Client.Socket.Recv←─────── Server.Socket.Send                      Server.Start       
     ↓			                              ↓                                  ↑                                      ↓                
Client.Socket.New                         Client.Message.Derialize           Server.Message.Serialize                Server.Listener.New             
	 ↓	                                      ↓                                  ↑                                      ↓          
Client.Socket.Connect                     ServerMessage                      ServerMessage                           Server.Listener.Bind       
	 ↓							              ↓                                  ↑                                      ↓          
Client.Socket.StartRecv  Client.Output←─Client.MessageProcess              Server.MessageProcess─→Server.Output  Server.Listener.Accept  
	 ↓		                                  ↓                                  ↑                                      ↓          
     └┄┄┄┄┬┄UserInput──────→ClientMessage                      ClientMessage                           Server.Socket.StartRecv  
			   ↑    ┆  	                  ↓                                  ↑                                      ┆
               └┄┄┘                   Client.Message.Serialize           Server.Message.Deserialize                   ┆                                       
								              ↓                                  ↑                                      ┆
                                          Client.Socket.Send────────→Server.Socket.Recv←┄┄┄┄┄┄┄┄┄┄┄┄┘                               
                                                

┄┅┆┇
MessageFramework应用SSL工作流/数据流:				
                                     Client.Output
									      ↑
┌───────────────── Client.MessageProcess                                           ┌─────────────────────────────┐    
│                                        ↑                                                         ↓                                                          │
│                                   ServerMessage                                             ServerMessage                    Server.Start                     │
│					                      ↑                                                         ↓                               ↓                         │
│    Client.Start                   Client.Message.Derialize                                   Server.Message.Serialize         Server.Listener.New             │
│		   ↓		                      ↑                                                         ↓                               ↓                         │
│    Client.Socket.New			     Client.StreamSSL.DecryptOutput                             Server.StreamSSL.Encrypt         Server.Listener.Bind            │
│         ↓                             ↑                                                         ↓                               ↓                         │ 
│    Client.Socket.Connect          Client.StreamSSL.Decrypt                                   Server.StreamSSL.EncryptOutput   Server.Listener.Accept          │
│         ↓                             ↑                                                         ↓                               ↓                         │
│    Client.Socket.StartRecv        Client.StreamSSL.IsAuthenticated?←─Client.Socket.Recv←─Server.Socket.Send               Server.Socket.StartRecv         │
│         ↓			                  ↓                                                         ↑                               ↓                         │
│    Client.StreamSSL.Initialize─→Client.StreamSSL.InitToken                                 Server.StreamSSL.AcceptToken←─ Server.StreamSSL.Initialize     │
│			                              ↓                                                         ↑                                                          │
│			                         Client.Socket.Send────────→Server.Socket.Recv ─→Server.StreamSSL.IsAuthenticated?                                │
│						                  ↑                                                         ↓                                                          │ 
│                                   Client.StreamSSL.EncryptOutput                             Server.StreamSSL.Decrypt                                         │
│					                      ↑                                                         ↓                                                          │
│                                   Client.StreamSSL.Encrypt                                   Server.StreamSSL.DecryptOutput                                   │
│					                      ↑                                                         ↓                                                          │
│                                   Client.Message.Serialize                                   Server.Message.Deserialize                                       │
│					                      ↑                                                         ↓                                                          │
└────────────────→ ClientMessage                                              ClientMessage                                                    │
                                          ↑                                                         ↓                                                          │
                                     UserInput                                                  Server.MessageProcess──────────────────────┘
									                                                                 ↓ 
																                                Server.Output 
				
