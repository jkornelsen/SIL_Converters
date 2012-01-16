//
// Note: To check the contents of the ICU libraries, you can do this:
// readelf -Ws libicuuc.so
//
#ifndef __ICUTRANSLITEC_H__
#define __ICUTRANSLITEC_H__

#pragma once

#ifdef __cplusplus
//
// Build as C-style so that the symbols can be found without C++ name-mangling.
// Methods can still be implemented in the C++ language.
//
extern "C" {
#endif

    int IcuTranslitEC_ConverterNameList_start(void);
    char * IcuTranslitEC_ConverterNameList_next(void);
    char * IcuTranslitEC_GetDisplayName(char * strID);

    int IcuTranslitEC_Initialize(char * strConverterID);
    int IcuTranslitEC_PreConvert (int eInEncodingForm, int& eInFormEngine,
                        int eOutEncodingForm, int& eOutFormEngine,
                        int& eNormalizeOutput, bool bForward,
                        int nInactivityWarningTimeOut);
    int IcuTranslitEC_DoConvert (char * lpInBuffer, int nInLen,
                        char * lpOutBuffer, int& rnOutLen);

#ifdef __cplusplus
}   // Close the extern C.
#endif
#endif // ICUTRANSLITEC_H
