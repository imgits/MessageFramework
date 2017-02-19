#pragma once
#include "_SslStream.h"

class _SslServerStream : _SslStream
{
public:
	_SslServerStream();
	~_SslServerStream();
	bool InitCredential(WCHAR* CertName);
	bool InitCredential(WCHAR* CertFile, WCHAR* password);
};

