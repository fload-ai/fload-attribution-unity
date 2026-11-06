// C entry points for Fload.Attribution's IosBridge ([DllImport("__Internal")]).
// They hand the C strings to FloadAttributionUnityBridge (Swift, @objc) and
// return at once; the Swift SDK does its work on its own queue.
#import <Foundation/Foundation.h>
#include <stdlib.h>
#include <string.h>

// The header Xcode generates for the Swift code in Unity's UnityFramework target.
// FLOAD_SWIFT_HEADER lets the host test build name its own module's header.
#ifdef FLOAD_SWIFT_HEADER
#import FLOAD_SWIFT_HEADER
#else
#import <UnityFramework/UnityFramework-Swift.h>
#endif

static NSString *FloadString(const char *value) {
    return value ? [NSString stringWithUTF8String:value] : nil;
}

extern "C" {

void FloadAttribution_Configure(const char *key, const char *endpoint, int logLevel) {
    NSString *keyString = FloadString(key);
    if (keyString == nil) return;  // C# refuses an empty key before it gets here.
    [FloadAttributionUnityBridge configureWithKey:keyString endpoint:FloadString(endpoint) logLevel:logLevel];
}

void FloadAttribution_SetUserId(const char *userId) {
    NSString *userIdString = FloadString(userId);
    if (userIdString == nil) return;
    [FloadAttributionUnityBridge setUserId:userIdString];
}

// A malloc'd copy, which Unity's marshaller frees, or NULL before configure.
char *FloadAttribution_InstallReference(void) {
    NSString *reference = [FloadAttributionUnityBridge installReference];
    if (reference == nil) return NULL;
    return strdup(reference.UTF8String);
}

}
