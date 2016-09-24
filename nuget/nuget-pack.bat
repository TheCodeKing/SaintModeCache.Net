pushd ..\
set msbuild="C:\Program Files (x86)\MSBuild\14.0\Bin\MsBuild.exe"
%msbuild% src\SaintModeCache.Net.sln -P:Configuration=SignedRelease
NuGet pack Nuget/SaintModeCache.Net.nuspec -Prop Configuration=SignedRelease -o Nuget
popd