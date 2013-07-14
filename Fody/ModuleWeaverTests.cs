﻿using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Pdb;
using NUnit.Framework;

[TestFixture]
public class ModuleWeaverTests
{
    string beforeAssemblyPath;
    string afterAssemblyPath;
    Assembly assembly;

    public ModuleWeaverTests()
    {
        beforeAssemblyPath = Path.GetFullPath(@"..\..\..\AssemblyToProcess\bin\Debug\AssemblyToProcess.dll");
#if (!DEBUG)
        beforeAssemblyPath = beforeAssemblyPath.Replace("Debug", "Release");
#endif
        afterAssemblyPath = beforeAssemblyPath.Replace(".dll", "2.dll");
        var oldpdb = beforeAssemblyPath.Replace(".dll", ".pdb");
        var newpdb = beforeAssemblyPath.Replace(".dll", "2.pdb");
        File.Copy(beforeAssemblyPath, afterAssemblyPath, true);
        File.Copy(oldpdb, newpdb, true);

        var assemblyResolver = new MockAssemblyResolver
            {
                Directory = Path.GetDirectoryName(beforeAssemblyPath)
            };

        using (var symbolStream = File.OpenRead(newpdb))
        {
            var readerParameters = new ReaderParameters
                {
                    ReadSymbols = true,
                    SymbolStream = symbolStream,
                    SymbolReaderProvider = new PdbReaderProvider()
                };
            var moduleDefinition = ModuleDefinition.ReadModule(afterAssemblyPath, readerParameters);

            var weavingTask = new ModuleWeaver
                {
                    ModuleDefinition = moduleDefinition,
                    AssemblyResolver = assemblyResolver,
                };

            weavingTask.Execute();
            moduleDefinition.Write(afterAssemblyPath);
        }
        assembly = Assembly.LoadFile(afterAssemblyPath);
    }

    [Test]
    public void Simple()
    {
        var instance = GetInstance("Simple");
        var isDisposed = GetIsDisposed(instance);
        Assert.IsFalse(isDisposed);
        instance.Dispose();
        isDisposed = GetIsDisposed(instance);
        Assert.IsTrue(isDisposed);
    }
    [Test]
    public void WithManagedAndUnmanaged()
    {
        var instance = GetInstance("WithManagedAndUnmanaged");
        var isDisposed = GetIsDisposed(instance);
        Assert.IsFalse(isDisposed);
        instance.Dispose();
        isDisposed = GetIsDisposed(instance);

        Assert.IsTrue(isDisposed);
        Assert.IsTrue(instance.DisposeManagedCalled);
        Assert.IsTrue(instance.DisposeUnmanagedCalled);
    }
    [Test]
    public void WithManaged()
    {
        var instance = GetInstance("WithManaged");
        var isDisposed = GetIsDisposed(instance);
        Assert.IsFalse(isDisposed);
        instance.Dispose();
        isDisposed = GetIsDisposed(instance);

        Assert.IsTrue(isDisposed);
        Assert.IsTrue(instance.DisposeManagedCalled);
    }
    [Test]
    public void WithUnmanaged()
    {
        var instance = GetInstance("WithUnmanaged");
        var isDisposed = GetIsDisposed(instance);
        Assert.IsFalse(isDisposed);
        instance.Dispose();
        isDisposed = GetIsDisposed(instance);

        Assert.IsTrue(isDisposed);
        Assert.IsTrue(instance.DisposeUnmanagedCalled);
    }
    [Test]
    public void WithUnmanagedAndDisposableField()
    {
        var instance = GetInstance("WithUnmanagedAndDisposableField");
        var isDisposed = GetIsDisposed(instance);
        Assert.IsFalse(isDisposed);
        instance.Dispose();
        isDisposed = GetIsDisposed(instance);

        Assert.IsTrue(isDisposed);
        Assert.IsTrue(instance.DisposeUnmanagedCalled);
        Assert.IsNull(instance.DisposableField);
    }
    [Test]
    public void WhereFieldIsDisposableByBase()
    {
        var instance = GetInstance("WhereFieldIsDisposableByBase");
        var child = instance.Child;
        var isChildDisposed = GetIsDisposed(child);
        Assert.IsFalse(isChildDisposed);
        instance.Dispose();
        isChildDisposed = GetIsDisposed(child);
        Assert.IsTrue(isChildDisposed);
    }

    static dynamic GetIsDisposed(dynamic instance)
    {
        Type type = instance.GetType();
        FieldInfo fieldInfo = null;
        while (fieldInfo == null && type != null)
        {
            fieldInfo = type.GetField("isDisposed", BindingFlags.NonPublic | BindingFlags.Instance);
            type = type.BaseType;
        }
        return fieldInfo.GetValue(instance);
    }

    public dynamic GetInstance(string className)
    {
        var type = assembly.GetType(className, true);
        return Activator.CreateInstance(type);
    }

    [Test]
    public void PeVerify()
    {
        Verifier.Verify(beforeAssemblyPath, afterAssemblyPath);
    }
}