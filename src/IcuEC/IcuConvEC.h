// Created by Steve McConnel Feb 2, 2012 by copying and editing IcuTranslitEC.h
//
// Note: To check the contents of the ICU libraries, you can do this:
// readelf -Ws libicuuc.so
//
#ifndef __ICUCONVEC_H__
#define __ICUCONVEC_H__

#pragma once

#ifdef __cplusplus
// Build as C-style so that the symbols can be found without C++ name-mangling.
// Methods can still be implemented in the C++ language.
extern "C"
{
#endif
	__declspec(dllexport)
    int IcuConvEC_ConverterNameList_start(void);
	__declspec(dllexport)
    const char * IcuConvEC_ConverterNameList_next(void);
	__declspec(dllexport)
    const char * IcuConvEC_GetDisplayName(char * strID);

	__declspec(dllexport)
    int IcuConvEC_Initialize(char * strConverterID);
	__declspec(dllexport)
    int IcuConvEC_PreConvert(int eInEncodingForm, int & eInFormEngine,
		int eOutEncodingForm, int & eOutFormEngine,
		int & eNormalizeOutput, bool bForward, int nInactivityWarningTimeOut);
	__declspec(dllexport)
    int IcuConvEC_DoConvert(char * lpInBuffer, int nInLen, char * lpOutBuffer, int & rnOutLen);

#ifdef __cplusplus
}   // Close the extern C.
#endif
#endif // ICUCONVEC_H
