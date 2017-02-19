#pragma once
using namespace System;
using namespace System::IO;
using namespace System::Security::Cryptography::X509Certificates;
public ref class StreamSSL
{
private:
	X509Certificate2^ credential;
public:
	bool IsAuthenticated;
public:
	StreamSSL(Stream^ stream);
	void AuthenticateAsServer(X509Certificate2 x509Cert);
};

