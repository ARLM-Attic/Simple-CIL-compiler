/*
Copyright(c) 2009, Stefan Simek
Copyright(c) 2015, Vladyslav Taranov

MIT License

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TryAxis.RunSharp;
#if FEAT_IKVM
using IKVM.Reflection;
using IKVM.Reflection.Emit;
using Type = IKVM.Reflection.Type;
using MissingMethodException = System.MissingMethodException;
using MissingMemberException = System.MissingMemberException;
using DefaultMemberAttribute = System.Reflection.DefaultMemberAttribute;
using Attribute = IKVM.Reflection.CustomAttributeData;
using BindingFlags = IKVM.Reflection.BindingFlags;
#else
using System.Reflection;
using System.Reflection.Emit;
#endif

namespace TriAxis.RunSharp
{
    using Language.RunSharp.Properties;

    using Operands;

	public interface IStatement
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "0#g", Justification = "The 'g' is used throughout the library for 'CodeGen'")]
		void Emit(CodeGen g);
	}

	public abstract class Operand
	{
        internal static readonly Operand[] EmptyArray = { };

		#region Virtual methods
		internal virtual void EmitGet(CodeGen g)
		{
			throw new InvalidOperationException(string.Format(null, Messages.ErrOperandNotReadable, GetType()));
		}

		internal virtual void EmitSet(CodeGen g, Operand value, bool allowExplicitConversion)
		{
			throw new InvalidOperationException(string.Format(null, Messages.ErrOperandNotWritable, GetType()));
		}

		internal virtual void EmitAddressOf(CodeGen g)
		{
			throw new InvalidOperationException(string.Format(null, Messages.ErrOperandNotReferencible, GetType()));
		}

		internal virtual void EmitBranch(CodeGen g, BranchSet branchSet, Label label)
		{
			if (g == null)
				throw new ArgumentNullException(nameof(g));
			if (branchSet == null)
				throw new ArgumentNullException(nameof(branchSet));

			EmitGet(g);
			g.IL.Emit(branchSet.BrTrue, label);
		}

	    public abstract Type GetReturnType(ITypeMapper typeMapper);
        
	    internal virtual bool TrivialAccess => false;
	    internal virtual bool IsStaticTarget => false;
	    internal virtual bool SuppressVirtual => false;
	    internal virtual object ConstantValue => null;
	    internal virtual void AssignmentHint(Operand op) { }
		#endregion

		// emits the refrence to the operand (address-of for value types)
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "0#g", Justification = "The 'g' is used throughout the library for 'CodeGen'")]
		public void EmitRef(CodeGen g)
		{
			if (GetReturnType(g.TypeMapper).IsValueType)
				EmitAddressOf(g);
			else
				EmitGet(g);
		}

		#region Implicit conversions
		[DebuggerHidden]
		public static implicit operator Operand(Type type)
		{
			return new TypeLiteral(type);
		}

		[DebuggerHidden]
		public static implicit operator Operand(string value)
		{
			return new StringLiteral(value);
		}

		[DebuggerHidden]
		public static implicit operator Operand(bool value)
		{
			return new IntLiteral(typeof(bool), value ? 1 : 0);
		}

		[DebuggerHidden]
		public static implicit operator Operand(byte value)
		{
			return new IntLiteral(typeof(byte), value);
		}

		[DebuggerHidden]
		//[CLSCompliant(false)]
		public static implicit operator Operand(sbyte value)
		{
			return new IntLiteral(typeof(sbyte), value);
		}

		[DebuggerHidden]
		public static implicit operator Operand(short value)
		{
			return new IntLiteral(typeof(short), value);
		}

		[DebuggerHidden]
		//[CLSCompliant(false)]
		public static implicit operator Operand(ushort value)
		{
			return new IntLiteral(typeof(ushort), value);
		}

		[DebuggerHidden]
		public static implicit operator Operand(char value)
		{
			return new IntLiteral(typeof(char), value);
		}

		[DebuggerHidden]
		public static implicit operator Operand(int value)
		{
			return new IntLiteral(typeof(int), value);
		}

		[DebuggerHidden]
		//[CLSCompliant(false)]
		public static implicit operator Operand(uint value)
		{
			return new IntLiteral(typeof(uint), unchecked((int)value));
		}

		[DebuggerHidden]
		public static implicit operator Operand(long value)
		{
			return new LongLiteral(typeof(long), value);
		}

		[DebuggerHidden]
		//[CLSCompliant(false)]
		public static implicit operator Operand(ulong value)
		{
			return new LongLiteral(typeof(ulong), unchecked((long)value));
		}

		[DebuggerHidden]
		public static implicit operator Operand(float value)
		{
			return new FloatLiteral(value);
		}

		[DebuggerHidden]
		public static implicit operator Operand(double value)
		{
			return new DoubleLiteral(value);
		}

		[DebuggerHidden]
		public static implicit operator Operand(decimal value)
		{
			return new DecimalLiteral(value);
		}

		public static implicit operator Operand(Enum value)
		{
			return new EnumLiteral(value);
		}
		#endregion

		public static Operand FromObject(object operandOrLiteral)
		{
			if (operandOrLiteral == null)
				return null;

			Operand op = operandOrLiteral as Operand;
			if ((object)op != null)
				return op;

			Type type = operandOrLiteral as Type;
			if (type != null)
				return type;

			string str = operandOrLiteral as string;
			if (str != null)
				return str;

			System.Type t = operandOrLiteral.GetType();

			if (t.IsEnum)
				return new EnumLiteral((Enum)operandOrLiteral);

			if (t.IsPrimitive)
			{
				if (t == typeof(int))
					return (int)operandOrLiteral;
				if (t == typeof(uint))
					return (uint)operandOrLiteral;
				if (t == typeof(long))
					return (long)operandOrLiteral;
				if (t == typeof(ulong))
					return (ulong)operandOrLiteral;
				if (t == typeof(float))
					return (float)operandOrLiteral;
				if (t == typeof(double))
					return (double)operandOrLiteral;

				// all other types are converted to I4
				return new IntLiteral(t, ((IConvertible)operandOrLiteral).ToInt32(null));
			}

			if (t == typeof(decimal))
				return (decimal)operandOrLiteral;

			throw new InvalidOperationException(Messages.ErrInvalidOperand);
		}

		public Assignment Assign(Operand value)
		{
			return Assign(value, false);
		}

		public Assignment Assign(Operand value, bool allowExplicitConversion)
		{
			return new Assignment(this, value, allowExplicitConversion);
		}

		public IStatement AssignAdd(Operand value)
		{
			return Assign(Add(value));
		}

		public IStatement AssignSubtract(Operand value)
		{
			return Assign(Subtract(value));
		}

		public IStatement AssignMultiply(Operand value)
		{
			return Assign(Multiply(value));
		}

		public IStatement AssignDivide(Operand value)
		{
			return Assign(Divide(value));
		}

		public IStatement AssignModulus(Operand value)
		{
			return Assign(Modulus(value));
		}

		public IStatement AssignAnd(Operand value)
		{
			return Assign(BitwiseAnd(value));
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", Justification = "Checked, OK")]
		public IStatement AssignOr(Operand value)
		{
			return Assign(BitwiseOr(value));
		}

		public IStatement AssignXor(Operand value)
		{
			return Assign(Xor(value));
		}

		public IStatement AssignLeftShift(Operand value)
		{
			return Assign(LeftShift(value));
		}

		public IStatement AssignRightShift(Operand value)
		{
			return Assign(RightShift(value));
		}

		public IStatement Increment()
		{
			return new PrefixOperation(Operator.Increment, this);
		}

		public IStatement Decrement()
		{
			return new PrefixOperation(Operator.Decrement, this);
		}

		#region Comparisons
		public override bool Equals(object obj)
		{
			throw new InvalidOperationException();
		}

		public override int GetHashCode()
		{
			throw new InvalidOperationException();
		}

		public static Operand operator ==(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.Equality, left, right);
		}

		public Operand Eq(Operand value)
		{
			return new OverloadableOperation(Operator.Equality, this, value);
		}

		public static Operand operator !=(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.Inequality, left, right);
		}

		public Operand Ne(Operand value)
		{
			return new OverloadableOperation(Operator.Inequality, this, value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates", Justification = "Method *has* an alternative (LT)")]
		public static Operand operator <(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.LessThan, left, right);
		}

		public Operand Lt(Operand value)
		{
			return new OverloadableOperation(Operator.LessThan, this, value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates", Justification = "Method *has* an alternative (GT)")]
		public static Operand operator >(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.GreaterThan, left, right);
		}

		public Operand Gt(Operand value)
		{
			return new OverloadableOperation(Operator.GreaterThan, this, value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates", Justification = "Method *has* an alternative (GE)")]
		public static Operand operator >=(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.GreaterThanOrEqual, left, right);
		}

		public Operand Ge(Operand value)
		{
			return new OverloadableOperation(Operator.GreaterThanOrEqual, this, value);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates", Justification = "Method *has* an alternative (LE)")]
		public static Operand operator <=(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.LessThanOrEqual, left, right);
		}

		public Operand Le(Operand value)
		{
			return new OverloadableOperation(Operator.LessThanOrEqual, this, value);
		}
		#endregion

		#region Arithmetic Operations
		public static Operand operator +(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.Add, left, right);
		}

		public Operand Add(Operand value)
		{
			return new OverloadableOperation(Operator.Add, this, value);
		}

		public static Operand operator -(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.Subtract, left, right);
		}

		public Operand Subtract(Operand value)
		{
			return new OverloadableOperation(Operator.Subtract, this, value);
		}

		public static Operand operator *(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.Multiply, left, right);
		}

		public Operand Multiply(Operand value)
		{
			return new OverloadableOperation(Operator.Multiply, this, value);
		}

		public static Operand operator /(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.Divide, left, right);
		}

		public Operand Divide(Operand value)
		{
			return new OverloadableOperation(Operator.Divide, this, value);
		}

		public static Operand operator %(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.Modulus, left, right);
		}

		public Operand Modulus(Operand value)
		{
			return new OverloadableOperation(Operator.Modulus, this, value);
		}

		public static Operand operator &(Operand left, Operand right)
		{
			if ((object)left != null && left._logical)
			{
				left._logical = false;
				return left.LogicalAnd(right);
			}

			return new OverloadableOperation(Operator.And, left, right);
		}

		public Operand BitwiseAnd(Operand value)
		{
			return new OverloadableOperation(Operator.And, this, value);
		}

		public static Operand operator |(Operand left, Operand right)
		{
			if ((object)left != null && left._logical)
			{
				left._logical = false;
				return left.LogicalOr(right);
			}

			return new OverloadableOperation(Operator.Or, left, right);
		}

		public Operand BitwiseOr(Operand value)
		{
			return new OverloadableOperation(Operator.Or, this, value);
		}

		public static Operand operator ^(Operand left, Operand right)
		{
			return new OverloadableOperation(Operator.Xor, left, right);
		}

		public Operand Xor(Operand value)
		{
			return new OverloadableOperation(Operator.Xor, this, value);
		}

		public static Operand operator <<(Operand left, int right)
		{
			return new OverloadableOperation(Operator.LeftShift, left, right);
		}

		public Operand LeftShift(Operand value)
		{
			return new OverloadableOperation(Operator.LeftShift, this, value);
		}

		public static Operand operator >>(Operand left, int right)
		{
			return new OverloadableOperation(Operator.RightShift, left, right);
		}

		public Operand RightShift(Operand value)
		{
			return new OverloadableOperation(Operator.RightShift, this, value);
		}

		public static Operand operator +(Operand op)
		{
			return new OverloadableOperation(Operator.Plus, op);
		}

		public Operand Plus()
		{
			return new OverloadableOperation(Operator.Plus, this);
		}

		public static Operand operator -(Operand op)
		{
			return new OverloadableOperation(Operator.Minus, op);
		}

		public Operand Negate()
		{
			return new OverloadableOperation(Operator.Minus, this);
		}

		public static Operand operator !(Operand op)
		{
			return new OverloadableOperation(Operator.LogicalNot, op);
		}

		public Operand LogicalNot()
		{
			return new OverloadableOperation(Operator.LogicalNot, this);
		}

		public static Operand operator ~(Operand op)
		{
			return new OverloadableOperation(Operator.Not, op);
		}

		public Operand OnesComplement()
		{
			return new OverloadableOperation(Operator.Not, this);
		}

		public Operand Pow2()
		{
			return new SimpleOperation(this, OpCodes.Dup, OpCodes.Mul);
		}

		public Operand LogicalAnd(Operand other)
		{
			return Conditional(other, false);
		}

		public Operand LogicalOr(Operand other)
		{
			return Conditional(true, other);
		}

		public Operand PostIncrement()
		{
			return new PostfixOperation(Operator.Increment, this);
		}

		public Operand PostDecrement()
		{
			return new PostfixOperation(Operator.Decrement, this);
		}

		public Operand PreIncrement()
		{
			return new PrefixOperation(Operator.Increment, this);
		}

		public Operand PreDecrement()
		{
			return new PrefixOperation(Operator.Decrement, this);
		}

		public Operand IsTrue()
		{
			return new OverloadableOperation(Operator.True, this);
		}

		public Operand IsFalse()
		{
			return new OverloadableOperation(Operator.False, this);
		}
		#endregion

		#region Logical operations
		bool _logical;
        
	    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates", Justification = "The operator is provided for convenience, so that the && and || operators work correctly. It should not be invoked under any other circumstances.")]
		public static bool operator true(Operand op)
		{
			if ((object)op != null)
				op._logical = true;
			return false;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2225:OperatorOverloadsHaveNamedAlternates", Justification = "The operator is provided for convenience, so that the && and || operators work correctly. It should not be invoked under any other circumstances.")]
		public static bool operator false(Operand op)
		{
			if ((object)op != null)
				op._logical = true;
			return false;
		}
		#endregion
		#region Special operations
		public Operand Conditional(Operand ifTrue, Operand ifFalse)
		{
			return new Conditional(this, ifTrue, ifFalse);
		}

	    public Operand Cast(Type type)
		{
			return new Cast(this, type);
		}
		#endregion

		#region Member access
		internal virtual BindingFlags GetBindingFlags()
		{
			return BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		}
        
		internal static Type GetType(Operand op, ITypeMapper typeMapper)
		{
			if ((object)op == null)
				return null;

			return op.GetReturnType(typeMapper);
		}

		internal static Type[] GetTypes(Operand[] ops, ITypeMapper typeMapper)
		{
			if (ops == null)
				return null;

			Type[] types = new Type[ops.Length];
			for (int i = 0; i < ops.Length; i++)
				types[i] = (object)ops[i] == null ? null : ops[i].GetReturnType(typeMapper);

			return types;
		}

		/*FieldInfo FindField(string name, BindingFlags flags)
		{
			FieldInfo fi = Type.GetField(name, flags);

			if (fi == null)
				throw new MissingFieldException(Messages.ErrMissingField);

			return fi;
		}

		PropertyInfo FindProperty(string name, BindingFlags flags, Operand[] indexes)
		{
			Type[] types = Operand.GetTypes(indexes);
			ArrayUtils.ReduceIncompleteTypesToBase(types);

			if (name == null)
			{
				foreach (DefaultMemberAttribute dma in Attribute.GetCustomAttributes(Type, typeof(DefaultMemberAttribute)))
				{
					name = dma.MemberName;
					break;
				}
			}

			if (name == null)
				throw new InvalidOperationException(Messages.ErrMissingDefaultProperty);

			PropertyInfo pi = Type.UnderlyingSystemType.GetProperty(name, flags, null, null, types, null);

			if (pi == null)
				throw new MissingMemberException(Messages.ErrMissingProperty);

			return pi;
		}

		MethodInfo FindMethod(string name, BindingFlags flags, Operand[] args)
		{
			Type[] types = Operand.GetTypes(args);
			ArrayUtils.ReduceIncompleteTypesToBase(types);

			MethodInfo mi = Type.GetMethod(name, flags, null, types, null);

			if (mi == null)
				throw new MissingMethodException(Messages.ErrMissingMethod);

			return mi;
		}
		*/

		public ContextualOperand Field(string name, ITypeMapper typeMapper)
		{
			return new ContextualOperand(new Field((FieldInfo)typeMapper.TypeInfo.FindField(GetReturnType(typeMapper), name, IsStaticTarget).Member, this), typeMapper);
        }

		public ContextualOperand Property(string name, ITypeMapper typeMapper)
		{
			return Property(name, typeMapper, EmptyArray);
		}

		public ContextualOperand Property(string name, ITypeMapper typeMapper, params Operand[] indexes)
		{
			return new ContextualOperand(new Property(typeMapper.TypeInfo.FindProperty(GetReturnType(typeMapper), name, indexes, IsStaticTarget), this, indexes), typeMapper);
        }

		public ContextualOperand Invoke(string name, ITypeMapper typeMapper)
		{
			return Invoke(name, typeMapper, EmptyArray);
		}

		public ContextualOperand Invoke(string name, ITypeMapper typeMapper, params Operand[] args)
		{
			return new ContextualOperand(new Invocation(typeMapper.TypeInfo.FindMethod(GetReturnType(typeMapper), name, args, IsStaticTarget), this, args), typeMapper);
        }

		public ContextualOperand InvokeDelegate(ITypeMapper typeMapper)
		{
			return InvokeDelegate(typeMapper, EmptyArray);
		}

		public ContextualOperand InvokeDelegate(ITypeMapper typeMapper, params Operand[] args)
		{
			return Invoke("Invoke", typeMapper, args);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1043:UseIntegralOrStringArgumentForIndexers", Justification = "Intentional, to simulate standard indexer 'feel'")]
		public ContextualOperand this[ITypeMapper typeMapper, params Operand[] indexes]
		{
			get
			{
				if (GetReturnType(typeMapper).IsArray)
					return new ContextualOperand(new ArrayAccess(this, indexes), typeMapper);

				return Property(null, typeMapper, indexes);
			}
		}

		public Operand ArrayLength()
		{
			return new ArrayLength(this, false);
		}

		public Operand LongArrayLength()
		{
			return new ArrayLength(this, true);
		}
		#endregion

		public Operand Ref()
		{
			return new Reference(this);
		}

		class Reference : Operand
		{
		    readonly Operand _op;

		    public Reference(Operand op)
		    {
		        _op = op;
		    }

			internal override void EmitAddressOf(CodeGen g)
			{
				_op.EmitAddressOf(g);
			}

		    public override Type GetReturnType(ITypeMapper typeMapper) => _op.GetReturnType(typeMapper).MakeByRefType();
		}
	}
}
