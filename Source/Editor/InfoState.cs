using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ArcherEditorMod.Editor;

/// <summary>
/// A copy of an archer's own and editor lists of hair / particle infos (plain classes made of public fields), for
/// the undo history. Two states are equal when every field of every info is.
/// </summary>
public sealed class InfoState<T> where T : class
{
    private static readonly MethodInfo memberwiseClone =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

    public List<T>? Own { get; }
    public List<T>? Editor { get; }
    private readonly string signature;

    public InfoState(IEnumerable<T>? own, IEnumerable<T>? editor)
    {
        Own = Copy(own);
        Editor = Copy(editor);
        signature = $"own:{Sig(Own)}|editor:{Sig(Editor)}";
    }

    /// <summary>Fresh copies to hand back to the feature, so the state itself stays untouched by later edits.</summary>
    public (List<T>? Own, List<T>? Editor) Fresh() => (Copy(Own), Copy(Editor));

    private static List<T>? Copy(IEnumerable<T>? list) =>
        list?.Select(item => (T)memberwiseClone.Invoke(item, null)!).ToList();

    private static string Sig(List<T>? list) =>
        list == null ? "null" : string.Join(";", list.Select(item => string.Join(",", fields.Select(f => f.GetValue(item)?.ToString()))));

    public override bool Equals(object? other) => other is InfoState<T> state && state.signature == signature;

    public override int GetHashCode() => signature.GetHashCode();
}
