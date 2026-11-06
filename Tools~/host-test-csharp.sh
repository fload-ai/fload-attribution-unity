#!/bin/sh
# Compiles the package's C# outside Unity, against Unity's reference assemblies
# (downloaded from NuGet into a cache; they are not part of this repo):
#   - Runtime for iOS, Android and the Editor/other platforms (the #if branches),
#   - Editor (the build check), and the Example sample against Runtime,
# then runs the pure C# tests on .NET. Needs the .NET SDK (8 or later).
# Reference assemblies: UnityEngine.Modules 2021.3.33 (UnityEngine modules) and
# Unity3D.SDK 2021.1.14.1 (UnityEditor.dll). They lag current Unity versions, so
# a green run here is a compile check, not proof against Unity 6.
set -eu
cd "$(dirname "$0")/.."
cache="${FLOAD_UNITY_REF_CACHE:-$HOME/.cache/fload-unity-refs}"
mkdir -p "$cache"
fetch() { # id version
  if [ ! -d "$cache/$1.$2" ]; then
    curl -sfL -o "$cache/$1.$2.nupkg" "https://www.nuget.org/api/v2/package/$1/$2"
    mkdir -p "$cache/$1.$2" && unzip -q -o "$cache/$1.$2.nupkg" -d "$cache/$1.$2" && chmod -R u+rwX "$cache/$1.$2"
  fi
}
fetch UnityEngine.Modules 2021.3.33
fetch Unity3D.SDK 2021.1.14.1
engine="$cache/UnityEngine.Modules.2021.3.33/lib/netstandard2.0"
editor="$cache/Unity3D.SDK.2021.1.14.1/lib"
build() { dotnet build "Tests~/HostTests/$1" -nologo -v q -p:UnityEngineDir="$engine" -p:UnityEditorDir="$editor" ${2:+"$2"} -o "${3:-$(mktemp -d)}"; }
for defines in "UNITY_IOS" "UNITY_ANDROID" ""; do
  echo "== Runtime ${defines:-(Editor and other platforms)}"
  build Runtime.csproj "-p:UnityDefines=$defines"
done
echo "== Editor"; build Editor.csproj
echo "== Sample"; build Sample.csproj
echo "== Host tests"
out="$(mktemp -d)"
build Fload.Attribution.HostTests.csproj "" "$out"
dotnet "$out/Fload.Attribution.HostTests.dll"
