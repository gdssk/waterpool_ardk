// Copyright 2021 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Niantic.ARDK.Utilities
{
  internal static class _StaticMemberValidator
  {
#if MUST_VALIDATE_STATIC_MEMBERS
    private static readonly ConcurrentDictionary<FieldInfo, object> _fieldsToValidate =
      new ConcurrentDictionary<FieldInfo, object>();

    private static readonly object _collectionsToValidateLock = new object();
    private static readonly HashSet<FieldInfo> _collectionsToValidate = new HashSet<FieldInfo>();

    // Do not call this method directly. Use a _StaticMembersValidatorScope instead.
    internal static void _ForScopeOnly_CheckCleanState()
    {
      _FriendTypeAsserter.AssertCallerIs(typeof(_StaticMembersValidatorScope));

      try
      {
        foreach (var pair in _fieldsToValidate)
        {
          var field = pair.Key;
          var expectedValue = pair.Value;

          var value = field.GetValue(null);

          if (!object.Equals(value, expectedValue))
          {
            string message =
              "Field '" + field.DeclaringType.FullName + "." + field.Name +"' contains garbage.\n" +
              "Expected value: " + expectedValue + ".\n" +
              "Actual value: " + value + ".";

            throw new InvalidOperationException(message);
          }
        }

        _fieldsToValidate.Clear();

        lock (_collectionsToValidateLock)
        {
          foreach (var field in _collectionsToValidate)
          {
            var untypedValue = field.GetValue(null);

            if (untypedValue == null)
              continue;

            var collection = (IEnumerable)untypedValue;
            foreach (var value in collection)
            {
              string message =
                "Collection '" + field.DeclaringType.FullName + "." + field.Name + "' is not empty.";
              
              throw new InvalidOperationException(message);
            }
          }

          _collectionsToValidate.Clear();
        }
      } 
      catch
      {
        // If any test fails, we just nullify/restore all fields and clear all collections.
        foreach(var pair in _fieldsToValidate)
        {
          var field = pair.Key;
          var value = pair.Value;

          field.SetValue(null, value);
        }

        _fieldsToValidate.Clear();

        lock (_collectionsToValidateLock)
        {
          foreach(var fieldCollection in _collectionsToValidate)
          {
            var untypedCollection = fieldCollection.GetValue(null);
            if (untypedCollection == null)
              continue;

            var clearMethod =
              untypedCollection.GetType()
              .GetMethod
              (
                "Clear",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null
              );

            if (clearMethod != null)
              clearMethod.Invoke(untypedCollection, null);
          }

          _collectionsToValidate.Clear();
        }
        
        throw;
      }
    }
#endif
    
    // Use only with static fields.
    // Usage like: _StaticMemberValidator._FieldIsNullWhenScopeEnds(() => staticField);
    [Conditional("MUST_VALIDATE_STATIC_MEMBERS")]
    internal static void _FieldIsNullWhenScopeEnds<T>(Expression<Func<T>> expression)
      where T: class
    {
      _FieldContainsSpecificValueWhenScopeEnds(expression, null);
    }

    // Use only with static fields.
    // Usage like:
    //   _StaticMemberValidator._FieldContainsSpecificValueWhenScopeEnds(() => staticField, 123);
    [Conditional("MUST_VALIDATE_STATIC_MEMBERS")]
    internal static void _FieldContainsSpecificValueWhenScopeEnds<T>
    (
      Expression<Func<T>> expression,
      T valueAtEnd
    )
    {
#if MUST_VALIDATE_STATIC_MEMBERS
      var memberExpression = (MemberExpression)expression.Body;
      var field = (FieldInfo)memberExpression.Member;

      if (!field.IsStatic)
        throw new InvalidOperationException("Given field is not static.");

      if (!_fieldsToValidate.TryAdd(field, valueAtEnd))
      {
        object existingValue;
        _fieldsToValidate.TryGetValue(field, out existingValue);

        if (!object.Equals(existingValue, valueAtEnd))
        {
          string message =
            "The field " + field.DeclaringType.FullName + "." + field.Name +
            " already has a valueAtEnd expectation to be '" + existingValue +
            "' but we are now trying to set a new expectation of '" + valueAtEnd + "'.";

          throw new InvalidOperationException(message);
        }
      }

      _StaticMembersValidatorScope._ForValidatorOnly_CheckScopeExists();
#endif
    }
    
    // Use only with static collection fields.
    // Usage like:
    //   _StaticMemberValidator._CollectionIsEmptyWhenScopeEnds(() => staticCollectionField);
    [Conditional("MUST_VALIDATE_STATIC_MEMBERS")]
    internal static void _CollectionIsEmptyWhenScopeEnds(Expression<Func<IEnumerable>> expression)
    {
#if MUST_VALIDATE_STATIC_MEMBERS
      var memberExpression = (MemberExpression)expression.Body;
      var field = (FieldInfo)memberExpression.Member;
      
      if (!field.IsStatic)
        throw new InvalidOperationException("Given field is not static.");

      lock (_collectionsToValidateLock)
        if (!_collectionsToValidate.Contains(field))
          _collectionsToValidate.Add(field);
      
      _StaticMembersValidatorScope._ForValidatorOnly_CheckScopeExists();
#endif
    }
  }
}
