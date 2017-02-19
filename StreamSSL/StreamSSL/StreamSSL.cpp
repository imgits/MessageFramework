#include "stdafx.h"
#include "StreamSSL.h"


StreamSSL::StreamSSL(Stream^ stream)
{
	//_Stream = stream;
	IsAuthenticated = false;
}

void StreamSSL::AuthenticateAsServer(X509Certificate2 x509Cert)
{

}