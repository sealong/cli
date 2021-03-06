// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.Testing.Abstractions
{
    public class SourceInformationProvider : ISourceInformationProvider
    {
        private readonly string _pdbPath;
        private readonly IPdbReader _pdbReader;

        public SourceInformationProvider(string pdbPath) :
            this(pdbPath, new PdbReaderFactory())
        {
        }

        public SourceInformationProvider(string pdbPath, IPdbReaderFactory pdbReaderFactory)
        {
            if (string.IsNullOrWhiteSpace(pdbPath) || !File.Exists(pdbPath))
            {
                throw new ArgumentException($"The file '{pdbPath}' does not exist.", nameof(pdbPath));
            }

            _pdbPath = pdbPath;

            _pdbReader = pdbReaderFactory.Create(pdbPath);
        }

        public SourceInformation GetSourceInformation(MethodInfo method)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            // We need a MethodInfo so we can deal with cases where no user code shows up for provided
            // method and class name. In particular:
            //
            // 1) inherited test methods (method.DeclaringType)
            // 2) async test methods (see StateMachineAttribute).t.
            //
            // Note that this doesn't deal gracefully with overloaded methods. Symbol APIs don't provide
            // a way to match overloads. We'd really need MetadataTokens to do this correctly (missing in
            // CoreCLR).
            method = ResolveBestMethodInfo(method);

            try
            {
                return _pdbReader.GetSourceInformation(method);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to access source information in symbol.", ex);
                return null;
            }
        }

        private MethodInfo ResolveBestMethodInfo(MethodInfo method)
        {
            // If a method has a StateMachineAttribute, then all of the user code will show up
            // in the symbols associated with the compiler-generated code. So, we need to look
            // for the 'MoveNext' on the generated type and resolve symbols for that.
            var attribute = method.GetCustomAttribute<StateMachineAttribute>();
            if (attribute?.StateMachineType == null)
            {
                return method;
            }

            return attribute.StateMachineType.GetMethod(
                "MoveNext",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public void Dispose()
        {
            _pdbReader.Dispose();
        }
    }
}