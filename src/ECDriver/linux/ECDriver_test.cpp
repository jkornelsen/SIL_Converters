//
// ECDriver_test.cpp
//
// Test code to verify that the ECDriver works correctly.
// Before running, manually create a converter called capsTest.
// Results will be given in STDERR, and other messages will be in STDOUT.
//
// Created by Jim Kornelsen on May 30 2013.
//

#include <stdio.h>
#include <string.h>
#include "ecdriver.h"

typedef bool (*funcTest_t)(void);

bool assertEqual(char * s1, char * s2, int len=-1)
{
    if (len == -1) {
        len = strlen(s2);
    }
    if (strncmp(s1, s2, len) == 0) {
        fprintf(stderr, "+");
        return true;
    } else {
        fprintf(stderr, "-");
        printf("'%s' != '%s'\n", s1, s2);
        return false;
    }
}
bool assertContains(char * s1, char * s2)
{
    if (strstr(s1, s2) == NULL) {
        fprintf(stderr, "-");
        printf("'%s' does not contain '%s'\n", s1, s2);
        return false;
    } else {
        fprintf(stderr, "+");
        return true;
    }
}

bool testCaps()
{
    if (IsEcInstalled())
    {
        const char sConverterName[] = "capsTest";
        const bool bDirectionForward = true;
        const int eNormFormOutput = 0;
        if (EncConverterInitializeConverter (
            sConverterName, bDirectionForward, eNormFormOutput) == 0)
        {
            char sDescription[1000];
            EncConverterConverterDescription(
                sConverterName, sDescription, 1000);
            //fprintf(stderr, "Description is %s.", sDescription);
            if (!assertContains(sDescription, (char *)"Name: 'capsTest'"))
                return false;

            // input data is unicode bytes (UTF-8)
            const char sInput[] = "abCde";
            char sOutput[1000];
            EncConverterConvertString (
                sConverterName, sInput, sOutput, 1000);
            // sOutput contains the unicode result (UTF-8)
            //fprintf(stderr, "Result is %s.", sOutput);
            if (!assertEqual(sOutput, (char *)"ABCDE")) return false;

            return true;
        }
    }
    return false;
}

// This test needs to be checked manually.
void testAutoSelect(void)
{
    char sConverterName[1000];
    bool bDirectionForward = true;
    int eNormFormOutput = 0;

    fprintf(stderr, "Calling SelectConverter.\n");
    int err = EncConverterSelectConverter (
              sConverterName, bDirectionForward, eNormFormOutput);
    if (err == 0) {
        printf("ok\n");
        printf("got %s\n", sConverterName);
    } else {
        printf("not ok\n");
    }
}

// Repeat calls many times to verify there are not memory problems.
bool testRepeat()
{
    for (int repeat = 0; repeat < 5; repeat++)
    {
        if (IsEcInstalled())
        {
//#define CALL_AUTOSELECT   // uncomment to call EncConverters.AutoSelect()
#ifdef CALL_AUTOSELECT 
            char sConverterName[1000];
            bool bDirectionForward = true;
            int eNormFormOutput = 0;
            if (EncConverterSelectConverter (
                sConverterName, bDirectionForward, eNormFormOutput) == 0)
#else
            const char sConverterName[] = "capsTest";
            const bool bDirectionForward = true;
            const int eNormFormOutput = 0;
            if (EncConverterInitializeConverter (
                sConverterName, bDirectionForward, eNormFormOutput) == 0)
#endif
            {
                for (int repeat = 0; repeat < 100; repeat++)
                {
                     // input data is unicode bytes (UTF-8)
                    const char sInput[] = "abCde";
                    char sOutput[1000];
                    EncConverterConvertString (
                        sConverterName, sInput, sOutput, 1000);
                    // sOutput contains the unicode result (UTF-8)
                    //fprintf(stderr, "Result is %s.", sOutput);
                    if (!assertEqual(sOutput, (char *)"ABCDE")) return false;

                    const char sInput2[] = "defgHi";
                    char sOutput2[1000];
                    EncConverterConvertString (
                        sConverterName, sInput2, sOutput2, 1000);
                    if (!assertEqual(sOutput2, (char *)"DEFGHI")) return false;
                }
            }
        }
        else
        {
            return false;
        }
    }
    return true;
}

int main(int argc, char **argv)
{
    int num_failed      = 0;
    const int NUM_TESTS = 2;
    funcTest_t testFunctions[NUM_TESTS];
    testFunctions[0] = &testCaps;
    testFunctions[1] = &testRepeat;
    for (int testnum = 0; testnum < NUM_TESTS; testnum++)
    {
        fprintf(stderr, "Running test %d... ", testnum);
        funcTest_t func = testFunctions[testnum];
        bool ok = (*func)();
        if (ok) {
            fprintf(stderr, " ok\n");
        } else {
            fprintf(stderr, " FAILED\n");
            num_failed++;
        }
    }
    fprintf(stderr, "\n\n"); // stdout results may perhaps be shown after this.
    if (num_failed == 0) {
        printf("All %d tests passed.\n", NUM_TESTS);
    } else {
        printf("%d of %d tests failed.\n", num_failed, NUM_TESTS);
    }
    return 0;
}
