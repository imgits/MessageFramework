#pragma once
#include "_SslStream.h"

class _SslServerStream : _SslStream
{
public:
	_SslServerStream();
	~_SslServerStream();
	bool AcceptClientToken(BYTE* ClientToken, int TokenSize);
};

