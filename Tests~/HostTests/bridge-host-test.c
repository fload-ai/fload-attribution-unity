// Drives the iOS bridge's C entry points (Plugins/iOS/FloadAttributionBridge.mm)
// the way Unity's [DllImport("__Internal")] does, on the host Mac, against the
// local stub ingest. Built and checked by Tools~/host-test-ios.sh.
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

void FloadAttribution_Configure(const char *key, const char *endpoint, int logLevel,
                               const char *wrapperName, const char *wrapperVersion);
void FloadAttribution_SetUserId(const char *userId);
char *FloadAttribution_InstallReference(void);

int main(int argc, char **argv) {
    if (argc < 5) {
        fprintf(stderr, "usage: bridge-host-test <endpoint> <user id> <wrapper name> <wrapper version>\n");
        return 2;
    }
    char *before = FloadAttribution_InstallReference();
    printf("reference before configure: %s\n", before ? before : "(null)");
    free(before);

    FloadAttribution_SetUserId(argv[2]);              // before configure: the SDK buffers it
    // The wrapper name and version FloadAttribution.cs passes (FloadAttribution.WrapperName, .Version).
    FloadAttribution_Configure("fload_pk_hosttest", argv[1], 4, argv[3], argv[4]);
    FloadAttribution_Configure("fload_pk_hosttest", argv[1], 4, argv[3], argv[4]);  // a second call is ignored

    char *reference = NULL;
    for (int i = 0; i < 50 && reference == NULL; i++) {
        usleep(100 * 1000);
        reference = FloadAttribution_InstallReference();
    }
    if (reference == NULL) {
        fprintf(stderr, "FAIL no install reference after configure\n");
        return 1;
    }
    printf("install reference: %s\n", reference);
    free(reference);
    sleep(3);
    FloadAttribution_SetUserId("unity-host-user-2");  // after the install: an identify request
    sleep(4);  // let the SDK's queue send (the token fetch and the first flush)
    return 0;
}
