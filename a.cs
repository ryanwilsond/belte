using System;

namespace Belte {

    public sealed class List<T> : Object {
        private T[]? _array;
        private int _length;
        private int _res;

        public List() : base() {
            this._length = 0;
            this._res = 0;
            this.TidyInit();
        }

        public List(int length) : base() {
            this._length = 0;
            this._res = 0;
            _array = new T[length];
            _length = length;
            _res = length;
        }

        public List(T[]? array) : base() {
            this._length = 0;
            this._res = 0;
            this.TidyInit();
            this.ConstructContents(array, ((global::System.Func<object, int?>)((x) => {{ return x is object[] y ? y.Length : null; }} ))(array).Value);
        }

        public List(List<T> list) : base() {
            this._length = 0;
            this._res = 0;
            this.TidyInit();
            this.ConstructContents(list._array, list._length);
        }

        public void Append(T value) {
            if ((_res > _length)) {
                _array[_length] = value;
                _length++;
                return;
            }

            int newCapacity = (_length + 10);
            T[]? newArray = new T[newCapacity];
            CopyInto(newArray, _array, 0, _length);
            _array = newArray;
            _length++;
            _res = newCapacity;
        }

        public void AppendRange(List<T> list) {
            int temp0 = (_length + list._length);
            if ((_res > temp0)) {
                CopyInto(_array, list._array, _length, list._length);
                _length += list._length;
                return;
            }

            int temp1 = (_length + list._length);
            int newCapacity = (temp1 + 10);
            T[]? newArray = new T[newCapacity];
            CopyInto(newArray, _array, 0, _length);
            CopyInto(newArray, list._array, _length, list._length);
            _array = newArray;
            _length += list._length;
            _res = newCapacity;
        }

        public void Pop() {
            if ((_length > 0)) {
                _length--;
            }

        }

        public void Clear() {
            _length = 0;
        }

        public void Assign(int index, T value) {
            index = this.PosIndex(index);
            if ((index == _length)) {
                this.Append(value);
                return;
            }
            else {
                if ((index > _length)) {
                    return;
                }

            }

            _array[index] = value;
        }

        public void Fill(T value) {
            for (int i = 0;
                (i < _length); i++) {
                _array[i] = value;
            }

        }

        public global::System.Nullable<int> Length() {
            return _length;
        }

        public T[]? ToArray() {
            T[]? copy = new T[_length];
            CopyInto(copy, _array, 0, _length);
            return copy ?? default(T[]?);
        }

        public T Index(int index) {
            index = this.PosIndex(index);
            bool temp0 = (index < 0);
            bool temp1 = (index >= _length);
            if ((temp0 || temp1)) {
                return default(T);
            }

            return _array[index] ?? default(T);
        }

        public List<T> Subset(int start, int end) {
            start = this.PosIndex(start);
            end = this.PosIndex(end);
            if ((end <= start)) {
                return new List<T>();
            }

            List<T> subset = new List<T>((end - start));
            for (int i = start;
                (i < end); i++) {
                subset.Append(_array[i]);
            }

            return subset;
        }

        public override string ToString() {
            if ((_length == 0)) {
                return "{ }";
            }

            string representation = "{ ";
            global::System.Nullable<bool> temp0 = (this is List<global::System.Nullable<int>>);
            global::System.Nullable<bool> temp1 = (this is List<string>);
            global::System.Nullable<bool> temp2 = ((temp0.HasValue && temp1.HasValue) ? (global::System.Nullable<bool>)(temp0.Value || temp1.Value) : null);
            global::System.Nullable<bool> temp3 = (this is List<global::System.Nullable<double>>);
            global::System.Nullable<bool> temp4 = ((temp2.HasValue && temp3.HasValue) ? (global::System.Nullable<bool>)(temp2.Value || temp3.Value) : null);
            global::System.Nullable<bool> temp5 = (this is List<global::System.Nullable<bool>>);
            global::System.Nullable<bool> temp6 = ((temp4.HasValue && temp5.HasValue) ? (global::System.Nullable<bool>)(temp4.Value || temp5.Value) : null);
            global::System.Nullable<bool> temp7 = (this is List<object>);
            global::System.Nullable<bool> temp8 = ((temp6.HasValue && temp7.HasValue) ? (global::System.Nullable<bool>)(temp6.Value || temp7.Value) : null);
            global::System.Nullable<bool> temp9 = (this is List<object>);
            global::System.Nullable<bool> temp10 = ((temp8.HasValue && temp9.HasValue) ? (global::System.Nullable<bool>)(temp8.Value || temp9.Value) : null);
            global::System.Nullable<bool> temp11 = (this is List<object>);
            if ((((temp10.HasValue && temp11.HasValue) ? (global::System.Nullable<bool>)(temp10.Value || temp11.Value) : null) ?? throw new global::System.NullReferenceException())) {
                for (global::System.Nullable<int> i = 0;
                    ((i.HasValue ? (global::System.Nullable<bool>)(i.Value < (_length - 1)) : null) ?? throw new global::System.NullReferenceException()); i++) {
                    representation += ((string)global::System.Convert.ToString(_array[i.Value]) is not null ? (string)((string)global::System.Convert.ToString(_array[i.Value]) + ", ") : null);
                }

                int temp12 = (_length - 1);
                object temp13 = _array[temp12];
                string temp14 = ((representation is not null && (string)global::System.Convert.ToString(temp13) is not null) ? (string)(representation + (string)global::System.Convert.ToString(temp13)) : null);
                return (temp14 is not null ? (string)(temp14 + " }") : null);
            }

            for (global::System.Nullable<int> i = 0;
                ((i.HasValue ? (global::System.Nullable<bool>)(i.Value < (_length - 1)) : null) ?? throw new global::System.NullReferenceException()); i++) {
                representation += (_array[i.Value].ToString() is not null ? (string)(_array[i.Value].ToString() + ", ") : null);
            }

            int temp15 = (_length - 1);
            Object temp16 = _array[temp15];
            string temp17 = temp16.ToString();
            string temp18 = ((representation is not null && temp17 is not null) ? (string)(representation + temp17) : null);
            return (temp18 is not null ? (string)(temp18 + " }") : null);
        }

        private void TidyInit() {
            _array = new T[10];
            _length = 0;
            _res = 10;
        }

        private void ConstructContents(T[]? array, int length) {
            if ((_res > length)) {
                CopyInto(_array, array, 0, length);
                _length = length;
                return;
            }

            int newCapacity = (length + 10);
            _array = new T[newCapacity];
            CopyInto(_array, array, 0, length);
            _length = length;
            _res = newCapacity;
        }

        private int PosIndex(int index) {
            if ((index < 0)) {
                index += _length;
            }

            return index;
        }

        private static void CopyInto(T[]? to, T[]? from, int start, int length) {
            for (int i = 0;
                (i < length); i++) {
                to[(i + start)] = from[i];
            }

        }

        public static T op_Index(List<T> list, global::System.Nullable<int> index) {
            if (!index.HasValue) {
                return default(T);
            }

            return list.Index(index.Value) ?? default(T);
        }

        public static T op_IndexAssign(List<T> list, global::System.Nullable<int> index, T value) {
            if (!index.HasValue) {
                return default(T);
            }

            list.Assign(index.Value, value);
            return list.Index(index.Value) ?? default(T);
        }

    }

}

namespace a {

    public static class Program {

        public class A : Object {

            public A() : base() {
            }

        }

        public class B : A {

            public B() : base() {
            }

        }

        public static void Main() { }

    }

}
