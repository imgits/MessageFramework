// StreamSSL.cpp: 主项目文件。

#include "stdafx.h"
#include <stdio.h>
#include "SslCredential.h"
using namespace System;

#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "secur32.lib")

int main(array<System::String ^> ^args)
{
	printf("Hello world\n");
	String^ msg = L"Hello World";
    Console::WriteLine(msg);
	SslCredential* pSslCredential = new SslCredential();
	pSslCredential->FindCertificate(L"localhost", L"E:/MessageFramework/SelfCert.pfx", L"messageframework");
    return 0;
}
