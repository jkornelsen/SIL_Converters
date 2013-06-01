/**
 * ECDriver.cpp
 * 
 * ECDriver is a library of unmanaged code that embeds Mono to call
 * the C# EncConverters core, starting with the EncConverters class in
 * SilEncConverters40.dll.
 * It implements the same interface as the Windows ECDriver.
 *
 * When building this file, be sure to link with mono-2.0
 *
 * Created by Jim Kornelsen on 29-Oct-2011.
 *
 * 25-Jan-2012 JDK  Specify MONO_PATH to find other assemblies.
 * 27-Mar-2012 JDK  Fixed bug: Maps require std::string to do string comparison.
 * 01-Jun-2013 JDK  Fixed crash: Don't call methGetMapByName from InitConv().
 */

#include "ecdriver.h"

#include <mono/jit/jit.h>
#include <mono/metadata/assembly.h>
#include <mono/metadata/debug-helpers.h>
#include <mono/metadata/mono-config.h>
#include <stdio.h>
#include <stdlib.h>
#include <string>       // for std::string
#include <cstring>      // for strcmp
#include <map>

//const char * ASSEMBLYFILE = "SilEncConverters40.dll";     // located in working directory
const char * ASSEMBLYFILE  = "/usr/lib/encConverters/SilEncConverters40.dll";
const char * ASSEMBLIESDIR = "MONO_PATH=/usr/lib/encConverters";  // look for other SEC assemblies here

// Uncomment the following line for verbose debugging output.
//#define VERBOSE_DEBUGGING

bool loaded=false;  // true when methods have been loaded
MonoDomain * domain                  = NULL;
MonoClass  * ecsClass                = NULL;
MonoObject * ecsObj                  = NULL;
MonoMethod * methAutoSelect          = NULL;
MonoMethod * methMakeWindowGoAway    = NULL;
MonoMethod * methGetDirectionForward = NULL;
MonoMethod * methSetDirectionForward = NULL;
MonoMethod * methGetNormalizeOutput  = NULL;
MonoMethod * methSetNormalizeOutput  = NULL;
MonoMethod * methGetConverterName    = NULL;
MonoMethod * methGetMapByName        = NULL;
MonoMethod * methToString            = NULL;
MonoMethod * methConvert             = NULL;
std::map<std::string, MonoObject *> mapECs;  // a map of EC objects
void * noArgs[0];   // when no arguments need to be passed

// Embed Mono, then load C# EncConverter classes and methods.
void LoadClasses(void)
{
    if (loaded) return;
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Loading Mono and SEC classes.\n");
#endif

    putenv((char *)ASSEMBLIESDIR);
    domain = mono_jit_init(ASSEMBLYFILE);
    mono_config_parse(NULL);  // prevents System.Drawing.GDIPlus exception
    MonoAssembly *assembly = mono_domain_assembly_open (domain, ASSEMBLYFILE);
    if (assembly) {
#ifdef VERBOSE_DEBUGGING
        fprintf(stderr, "ECDriver: Got assembly %s.\n", ASSEMBLYFILE);
#endif
    } else {
        fprintf(stderr, "ECDriver: Could not open assembly %s. Please verify the location.\n", ASSEMBLYFILE);
        return;
    }
    MonoImage* image = mono_assembly_get_image(assembly);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Got image\n");
#endif

    // Load class from assembly

    ecsClass = mono_class_from_name (
               image, "SilEncConverters40", "EncConverters");
    if (ecsClass == NULL) {
        fprintf(stderr, "ECDriver: Could not get the class. Perhaps ECInterfaces.dll is missing.\n");
        return;
    }
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Got class handle\n");
#endif
    // This line will crash if ECInterfaces.dll is not found.
    ecsObj = mono_object_new(domain, ecsClass);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Calling EncConverters constructor.\n");
#endif
    mono_runtime_object_init(ecsObj);   // call SEC constructor
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Finished calling ECs constructor.\n");
#endif

    // Get method pointers from class

    void * iter = NULL;
    MonoMethod * m = NULL;
    while ((m = mono_class_get_methods (ecsClass, &iter))) {
        const char * methName = mono_method_get_name(m);
#ifdef VERBOSE_DEBUGGING
        //fprintf(stderr, "    method %s\n", methName);
#endif
        if (strcmp (methName, "AutoSelect") == 0) {
            methAutoSelect = m;
        } else if (strcmp (methName, "MakeSureTheWindowGoesAway") == 0) {
            methMakeWindowGoAway = m;
        } else if (strcmp (methName, "get_Item") == 0) {
            // In EncConverters.cs, the declaration is:
            // public new IEncConverter this[object mapName]
            methGetMapByName = m;
        }
    }
    if (methAutoSelect == NULL) {
        fprintf(stderr, "ECDriver: Error! Could not get method(s).\n");
        return;
    }

    MonoClass * ecClass = mono_class_from_name(
                          image, "SilEncConverters40", "EncConverter");
    iter = NULL;
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Getting EncConverter class methods.\n");
#endif
    while ((m = mono_class_get_methods (ecClass, &iter))) {
        const char * methName = mono_method_get_name(m);
#ifdef VERBOSE_DEBUGGING
        //fprintf(stderr, "    method %s\n", methName);
#endif
        if (strcmp (methName, "get_Name") == 0) {
            methGetConverterName = m;
        } else if (strcmp (methName, "get_DirectionForward") == 0) {
            methGetDirectionForward = m;
        } else if (strcmp (methName, "set_DirectionForward") == 0) {
            methSetDirectionForward = m;
        } else if (strcmp (methName, "get_NormalizeOutput") == 0) {
            methGetNormalizeOutput = m;
        } else if (strcmp (methName, "set_NormalizeOutput") == 0) {
            methSetNormalizeOutput = m;
        } else if (strcmp (methName, "ToString") == 0) {
            methToString = m;
        } else if (strcmp (methName, "Convert") == 0) {
            methConvert = m;
        }
    }
    if (methConvert == NULL) {
        fprintf(stderr, "ECDriver: Error! Could not get method(s).\n");
        return;
    }
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Got methods.\n");
#endif

    loaded = true;
}

void Cleanup(void)
{
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: cleanup BEGIN\n");
#endif
    if (!loaded) return;
    loaded=false;
    mono_jit_cleanup(domain);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: cleanup END\n");
#endif
}

bool IsEcInstalled(void)
{
    LoadClasses();
    return loaded;
}

MonoObject * GetEncConverter(const char * sConverterName)
{
    if (mapECs.find(sConverterName) != mapECs.end())
    {
#ifdef VERBOSE_DEBUGGING
        fprintf(stderr, "ECDriver: Got converter %s.\n", sConverterName);
#endif
        return mapECs[sConverterName];
    }
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Couldn't get converter %s.\n", sConverterName);
    fprintf(stderr, "ECDriver: Available converters: ");
    std::map<std::string, MonoObject *>::iterator i = mapECs.begin();
    for( ; i != mapECs.end(); ++i )
    {
        fprintf(stderr, "'%s', ", i->first.c_str());
    }
    fprintf(stderr, "\n");
    fprintf(stderr, "ECDriver: Getting map..\n");
#endif
    MonoString * mConverterName = mono_string_new (domain, sConverterName);
    void * args1[1];
    args1[0] = mConverterName;
    MonoObject * pEC = mono_runtime_invoke(
                       methGetMapByName, ecsObj, args1, NULL);
    if (pEC != NULL)
    {
        mapECs[sConverterName] = pEC;
        return pEC;
    }
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Did not find map.\n");
#endif
    return 0;
}

int EncConverterSelectConverter (
    char * sConverterName, bool & bDirectionForward, int & eNormOutputForm)
{
    LoadClasses();
    if (!loaded) return -1;

    int convType = 0;   // convType Unknown, defined in ECInterfaces.cs
    void * args1[1];
    args1[0] = &convType;
    MonoObject *pEC = mono_runtime_invoke(
                         methAutoSelect, ecsObj, args1, NULL);
    mono_runtime_invoke(methMakeWindowGoAway, ecsObj, noArgs, NULL);
    if (pEC == NULL) {
        fprintf(stderr, "ECDriver: Did not get converter.\n");
        return -1;
    }
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Got converter.\n");
#endif

    MonoString * mConverterName = (MonoString*) mono_runtime_invoke (
                                  methGetConverterName, pEC, noArgs, NULL);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: invoked methGetConverterName.\n");
#endif
    char * sTemp = mono_string_to_utf8(mConverterName);
    strcpy(sConverterName, sTemp);
    mono_free(sTemp);
#ifdef VERBOSE_DEBUGGING
    fprintf (stderr, "ECDriver: Converter name is '%s'\n", sConverterName);
#endif

    MonoObject * mResult = mono_runtime_invoke (
                           methGetDirectionForward, pEC, noArgs, NULL);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: invoked methGetDirectionForward.\n");
#endif
    bDirectionForward = *(bool*)mono_object_unbox(mResult);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Value of bool is: %s\n",
            (bDirectionForward)?"true":"false");
#endif

    mResult = mono_runtime_invoke (
              methGetNormalizeOutput, pEC, noArgs, NULL);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: invoked methGetNormalizeOutput.\n");
#endif
    eNormOutputForm = *(int*) mono_object_unbox(mResult);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Value of int is: %d\n", eNormOutputForm);
#endif

    mapECs[sConverterName] = pEC;

    return 0;
}

int EncConverterInitializeConverter(
    const char * sConverterName, bool bDirectionForward, int eNormOutputForm)
{
    LoadClasses();
    if (!loaded) return -1;

    MonoObject * pEC = GetEncConverter(sConverterName);
    if (pEC == 0)
        return /*NameNotFound*/ -7;
 
    void * args2[1];
    args2[0] = &bDirectionForward;
    mono_runtime_invoke(methSetDirectionForward, pEC, args2, NULL);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: invoked methSetDirectionForward.\n");
#endif

    void * args3[1];
    args3[0] = &eNormOutputForm;
    mono_runtime_invoke(methSetNormalizeOutput, pEC, args3, NULL);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: invoked methSetNormalizeOutput.\n");
#endif

    return 0;
}

/*
 When calling this function, provide a writable buffer for sOutput.
 If the result is bigger than nOutputLen, the result will be truncated.
 */
int EncConverterConvertString (
    const char * sConverterName,
    const char * sInput,
    char *       sOutput,
    int          nOutputLen)
{
    LoadClasses();
    if (!loaded) return -1;

    MonoObject * pEC = GetEncConverter(sConverterName);
    if (pEC == 0)
        return /*NameNotFound*/ -7;
 
    MonoString * mInput = mono_string_new(domain, sInput);
    void * args1[1];
    args1[0] = mInput;
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Calling methConvert.\n");
#endif
    MonoString * mOutput = (MonoString *) mono_runtime_invoke (
                           methConvert, pEC, args1, NULL);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: Method invoked.\n");
#endif
    char * sTemp = mono_string_to_utf8(mOutput);
    strncpy(sOutput, sTemp, nOutputLen);
    sOutput[nOutputLen - 1] = '\0'; // make sure string is null-terminated
    mono_free(sTemp);
#ifdef VERBOSE_DEBUGGING
    //fprintf(stderr, "ECDriver: Result is: %s\n", sOutput);
    fprintf(stderr, "ECDriver: Got result.\n");
#endif
    return 0;
}

int EncConverterConverterDescription (
    const char * sConverterName, char * sDescription, int nDescriptionLen)
{
    LoadClasses();
    if (!loaded) return -1;

    MonoObject * pEC = GetEncConverter(sConverterName);
    if (pEC == 0)
        return /*NameNotFound*/ -7;

    MonoString * mDescription = (MonoString *) mono_runtime_invoke (
                                methToString, pEC, noArgs, NULL);
#ifdef VERBOSE_DEBUGGING
    fprintf(stderr, "ECDriver: invoked methToString.\n");
#endif
    char * sTemp = mono_string_to_utf8(mDescription);
    strncpy(sDescription, sTemp, nDescriptionLen);
    sDescription[nDescriptionLen - 1] = '\0'; // make sure string is null-terminated
    mono_free(sTemp);
#ifdef VERBOSE_DEBUGGING
    //fprintf(stderr, "ECDriver: Result is: %s\n", sDescription);
    fprintf(stderr, "ECDriver: got description.\n");
#endif

    return 0;
}
