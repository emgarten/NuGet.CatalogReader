using System;
using System.Collections.Generic;
using System.Text;
using Test.Common;

namespace NuGetMirror.CmdExe.Tests
{
    public static class ExeUtils
    {
        private static readonly Lazy<string> _getExe = new Lazy<string>(() => CmdRunner.GetPath("artifacts/publish/NuGetMirror.exe"));

        public static string NuGetMirrorExePath => _getExe.Value;
    }
}
