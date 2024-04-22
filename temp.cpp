// NSLib vector header
#pragma once
#ifndef _NSVECTOR_
#define _NSVECTOR_

// #define NSTD_USE_STD_OVERLAY

#include <xnsdef>
#include <xnsmem0>
#include <xnsinit_list>

using _NSTD initializer_list;
using _NSTD allocator;

// CLASS TEMPLATE _Vector_iterator
template <class _Ty>
class _Vector_iterator
{ // iterator for vector
private:
    using pointer = _Ty *;
    using value_type = _Ty;
    using size_type = _NSTD size_t;

    const pointer _MY_PTR;
    size_type _MY_POS;

public:
    _Vector_iterator() noexcept
    {
    }

    _Vector_iterator(pointer _Ptr) noexcept
        : _MY_PTR(_Ptr)
    {
        this->_MY_POS = 0;
    }

    _Vector_iterator(const _Vector_iterator &_Vecit) noexcept
        : _MY_PTR(_Vecit._Myptr())
    {
        this->_MY_POS = _Vecit._Mypos();
    }

    ~_Vector_iterator()
    {
    }

    // access methods

    pointer _Myptr() const
    {
        return this->_MY_PTR;
    }

    size_type _Mypos() const
    {
        return this->_MY_POS;
    }

    // operators

    int operator==(const _Vector_iterator &_Right) const
    {
        return this->comparePtr(_Right);
    }

    int operator!=(const _Vector_iterator &_Right) const
    {
        if (this->comparePtr(_Right))
        {
            return 0;
        }

        return 1;
    }

    value_type operator*() const
    {
        return this->_Myptr()[this->_Mypos()];
    }

    void operator++()
    {
        this->next();
    }

    // public methods

    int comparePtr(const _Vector_iterator &_Right) const
    {
        auto _Myloc = this->_Myptr() + this->_Mypos() * sizeof(value_type);
        auto _Right_loc = _Right._Myptr() + _Right._Mypos() * sizeof(value_type);

        if (_Myloc == _Right_loc)
        {
            return 1;
        }

        return 0;
    }

    int comparePos(const _Vector_iterator &_Right) const
    {
        if (this->_Mypos() == _Right._Mypos())
        {
            return 1;
        }

        return 0;
    }

    void position(size_type _Pos)
    {
        this->_MY_POS = _Pos;
    }

    void next()
    {
        this->_MY_POS++;
    }
};

// NSTD_USE_STD_OVERLAY switches to a different vector definition which uses stl under the hood (for debugging)
// Not recommended for normal use
#ifndef NSTD_USE_STD_OVERLAY

// CLASS TEMPLATE _Vector_alloc
template <class _Ty,
          class _Alloc = allocator<_Ty>>
class _Vector_alloc
{ // base class for vector to hold allocator
public:
    using _Alty = _Alloc;

    using value_type = _Ty;
    using pointer = _Ty *;
    using const_pointer = const _Ty *const;
    using size_type = _NSTD size_t;
    using iterator = _Vector_iterator<_Ty>;

private:
    _Alty _AL;

public:
    _Vector_alloc() noexcept
    {
        this->_AL = _Alloc();
    }

    _Vector_alloc(const _Alty &_Al) noexcept
    {
        this->_Moveal(_Al);
    }

    ~_Vector_alloc()
    {
    }

    void _Moveal(const _Alty &_Al)
    {
        this->_AL = _Al;
    }

    _Alty _Getal() const
    {
        return this->_AL;
    }
};

// CLASS TEMPLATE vector
template <class _Ty,
          class _Alloc = allocator<_Ty>>
class vector
    : public _Vector_alloc<_Ty, _Alloc>
{ // varying size array of values
private:
    using _Mybase = _Vector_alloc<_Ty, _Alloc>;
    using _Alty = typename _Mybase::_Alty;

    using value_type = typename _Mybase::value_type;
    using pointer = typename _Mybase::pointer;
    using const_pointer = typename _Mybase::const_pointer;
    using size_type = typename _Mybase::size_type;
    using iterator = typename _Mybase::iterator;

    using reference = value_type &;
    using const_reference = const value_type &;

    value_type _Null_value = value_type();

    pointer _MY_PTR;
    size_type _MY_SIZE;
    size_type _MY_RES = 0;

public:
    vector() noexcept
        : _Mybase()
    {
        this->_Tidy_init();
    }

    vector(initializer_list<value_type> _Ilist) noexcept
        : _Mybase()
    {
        this->_Tidy_init();
        this->_Construct_contents(_Ilist.begin(), _Ilist.size());
    }

    vector(initializer_list<value_type> _Ilist, const _Alty &_Al) noexcept
        : _Mybase(_Al)
    {
        this->_Tidy_init();
        this->_Construct_contents(_Ilist.begin(), _Ilist.size());
    }

    vector(const vector &_Right) noexcept
        : _Mybase()
    {
        this->_Tidy_init();
        this->_Construct_contents(_Right);
    }

    vector(const vector &_Right, const _Alty &_Al) noexcept
        : _Mybase(_Al)
    {
        this->_Tidy_init();
        this->_Construct_contents(_Right);
    }

    vector(_In_reads_(_Size) const_pointer _Right, const size_type _Size) noexcept
        : _Mybase()
    {
        this->_Tidy_init();
        this->_Construct_contents(_Right, _Size);
    }

    vector(_In_reads_(_Size) const_pointer _Right, const size_type _Size, const _Alty &_Al) noexcept
        : _Mybase(_Al)
    {
        this->_Tidy_init();
        this->_Construct_contents(_Right, _Size);
    }

    explicit vector(const _Alty &_Al) noexcept
        : _Mybase(_Al)
    {
    }

    ~vector()
    {
        this->_Getal().deallocate(this->_Myptr(), this->_Myres());
    }

    void operator=(const vector &_Right)
    {
        if (this != _NSTD addressof(_Right))
        { // no need to deallocate, just overwrite
            this->_Moveal(_Right._Getal());
            this->_Construct_contents(_Right);
        }
    }

    // "private" (only used by vector even if another instance)

    void _Tidy_init()
    {
        this->_MY_RES = 10;
        this->_MY_PTR = this->_Getal().allocate(this->_Myres()); // buffer
        this->_MY_SIZE = 0;
    }

    void _Tidy_deallocate()
    {
        if (this->_Myres() > 0)
        {
            this->_Getal().deallocate(this->_Myptr(), this->_Myres());
            this->_MY_SIZE = 0;
            this->_MY_RES = 0;
        }
    }

    void _Construct_contents(const vector &_Right)
    {
        this->_Construct_contents(_Right._Myptr(), _Right._Mysize());
    }

    void _Construct_contents(const_pointer _Start, size_type _Size)
    {
        if (this->_Myres() > _Size)
        {
            _NSTD copyinto(this->_Myptr(), _Start, _Size);
            this->_MY_SIZE = _Size;
            return;
        }

        const size_type _New_capacity = _Size + 10;
        pointer _New_array = this->_Getal().allocate(_New_capacity);
        this->_Getal().deallocate(this->_Myptr(), this->_Myres());
        this->_MY_PTR = _New_array;

        _NSTD copyinto(this->_Myptr(), _Start, _Size);
        this->_MY_SIZE = _Size;
        this->_MY_RES = _New_capacity;
    }

    // access methods

    pointer _Myptr() const
    {
        return this->_MY_PTR;
    }

    size_type _Mysize() const
    {
        return this->_MY_SIZE;
    }

    size_type _Myres() const
    {
        return this->_MY_RES;
    }

    // operators

    value_type &operator[](size_type _Index) const
    {
        return this->index(_Index);
    }

    vector operator+(const vector &_Right) const
    {
        vector _Total;

        if (this->_Mysize() > 0)
        {
            for (size_type i = 0; i < this->_Mysize(); i++)
            {
                _Total.append(this->_Myptr()[i]);
            }
        }

        if (_Right._Mysize() > 0)
        {
            for (value_type _El : _Right)
            {
                _Total.append(_El);
            }
        }

        return _Total;
    }

    // public methods

    void append(value_type _Value)
    {
        // consider rewrite to put allocation inside if
        if (this->_Myres() > this->_Mysize())
        {
            this->_Myptr()[this->_Mysize()] = _Value;
            this->_MY_SIZE++;
            return;
        }

        const size_type _New_capacity = this->_Mysize() + 10;
        pointer _New_array = this->_Getal().allocate(_New_capacity);
        _NSTD copyinto(_New_array, this->_Myptr(), this->_Mysize());

        // crash here
        this->_Getal().deallocate(this->_Myptr(), this->_Myres());
        this->_MY_PTR = _New_array;
        this->_Myptr()[this->_Mysize()] = _Value;

        this->_MY_SIZE++;
        this->_MY_RES = _New_capacity;
    }

    void pop()
    {
        this->_MY_SIZE--; // doesn't actually remove the element
    }

    void pop(size_type _Index)
    {
        size_type j, in;
        for (j = 0, in = 0; j < this->_Mysize(); j++, in++)
        {
            if (j == _Index)
            {
                in--;
                continue;
            }

            this->_Myptr()[in] = this->_Myptr()[j];
        }

        this->_MY_SIZE--;
    }

    value_type &index(long long _Index) const
    {
        const size_type _Pos = _NSTD posindex(_Index, this->_Mysize());
        return &this->_Myptr()[_Pos];
    }

    inline size_type count() const
    {
        return this->_Mysize();
    }

    size_type size() const
    {
        return this->_Mysize() * sizeof(value_type);
    }

    pointer to_array() const
    {
        pointer _Copy = this->_Getal().allocate(this->_Mysize());

        for (size_type i = 0; i < this->_Mysize(); i++)
        {
            _Copy[i] = this->_Myptr()[i];
        }

        return _Copy;
    }

    void insert(long long _Index, value_type _Val)
    {
        const size_type _Pos = _NSTD posindex(_Index, this->_Mysize());

        if (this->_Myres() <= this->_Mysize())
        {
            const size_type _New_capacity = this->_Mysize() + 10;
            pointer _New_array = this->_Getal().allocate(_New_capacity);
            _NSTD copyinto(_New_array, this->_Myptr(), this->_Mysize());

            this->_Getal().deallocate(this->_Myptr(), this->_Myres());
            this->_MY_PTR = _New_array;

            this->_MY_RES = _New_capacity;
        }

        for (size_type i = this->_Mysize(); i >= 0; i--)
        {
            if (i != _Pos)
            {
                this->_Myptr()[i] = this->_Myptr()[i - 1];
            }
            else
            {
                this->_Myptr()[i] = _Val;
                break;
            }
        }

        this->_MY_SIZE++;
    }

    iterator begin() const
    {
        iterator _It(this->_Myptr());
        return _It;
    }

    iterator end() const
    {
        iterator _It(this->_Myptr());
        _It.position(this->_Mysize());
        return _It;
    }

    pointer data() const
    {
        return this->_Myptr();
    }

    void assign(const size_type _Index, value_type _Right)
    {
        if (_Index == this->_Mysize())
        {
            this->append(_Right);
            return;
        }
        else if (_Index > this->_Mysize())
        {
            return; // outside bounds + 1
        }

        this->_Myptr()[_Index] = _Right;
    }

    vector subset(const long long _Start, const long long _End = -1)
    {
        const size_type _Start_u = _NSTD posindex(_Start, this->count());
        const size_type _End_u = _NSTD posindex(_End, this->count());

        vector<value_type> _Subset;

        for (size_type i = _Start_u; i < _End_u; i++)
        {
            _Subset.append(this->index(i));
        }

        return _Subset;
    }

    void clear() const
    {
        this->_MY_SIZE = 0;
    }
};

#endif // NSTD_USE_STD_OVERLAY

#ifdef NSTD_USE_STD_OVERLAY

#include <vector>

// CLASS TEMPLATE vector
template <class _Ty>
class vector
{ // overlay of stl vector for debugging purposes
private:
    using iterator = _Vector_iterator<_Ty>;
    _STDA vector<_Ty> _MY_VEC;

public:
    vector()
    {
    }

    vector(initializer_list<_Ty> _Ilist)
    {
        this->_MY_VEC = (_STDA vector<_Ty>)_Ilist;
    }

    vector(const vector &_Right)
    {
        this->_MY_VEC = _Right._Myvec();
    }

    vector(const _Ty *_Right, const _NSTD size_t _Size)
    {
        this->_MY_VEC = _STDA vector<_Ty>(_Size, _Right);
    }

    void operator=(const vector &_Right)
    {
        this->_MY_VEC = _Right._Myvec();
    }

    _STDA vector<_Ty> _Myvec() const
    {
        return this->_MY_VEC;
    }

    _NSTD size_t count() const
    {
        return this->_MY_VEC.size();
    }

    _NSTD size_t size() const
    {
        return this->_MY_VEC.size() * sizeof(_Ty);
    }

    vector operator+(const vector &_Right) const
    {
        vector<_Ty> _Dup;

        for (_NSTD size_t i; i < this->count(); i++)
        {
            _Dup.append(this->_MY_VEC[i]);
        }

        for (_Ty elem : _Right)
        {
            _Dup.append(elem);
        }

        return _Dup;
    }

    _Ty operator[](const long long _Index) const
    {
        return this->index(_Index);
    }

    _Ty index(const long long _Index) const
    {
        _NSTD size_t _Index_abs = _Index;
        if (_Index < 0)
            _Index_abs += this->count();
        return this->_MY_VEC.at(_Index_abs);
    }

    void append(const _Ty _Right)
    {
        this->_MY_VEC.push_back(_Right);
    }

    _Ty *to_array() const
    {
        _Ty *_Copy = new _Ty[this->count()];

        for (_NSTD size_t i = 0; i < this->count(); i++)
        {
            _Copy[i] = this->_MY_VEC[i];
        }

        return _Copy;
    }

    void pop(const long long _Index = -1)
    {
        _NSTD size_t _Index_abs = _Index;
        if (_Index < 0)
            _Index_abs += this->count();
        this->_MY_VEC.erase(this->_MY_VEC.begin() + _Index_abs);
    }

    iterator begin() const
    {
        iterator _It((_Ty *)this->_MY_VEC.data());
        return _It;
    }

    iterator end() const
    {
        iterator _It((_Ty *)this->_MY_VEC.data());
        _It.position(this->count());
        return _It;
    }

    _Ty *data() const
    {
        return this->_MY_VEC.data();
    }

    void assign(const _NSTD size_t _Index, const _Ty _Right)
    {
        this->_MY_VEC.at(_Index) = _Right;
    }

    void insert(const long long _Index, const _Ty _Right)
    {
        _NSTD size_t _Index_abs = _Index;
        if (_Index < 0)
            _Index_abs += this->count();
        this->_MY_VEC.insert(this->_MY_VEC.begin() + _Index_abs, _Right);
    }

    vector subset(const long long _Start, const long long _End = -1)
    {
        _NSTD size_t _Start_u;
        _NSTD size_t _End_u;

        if (_Start < 0)
        {
            _Start_u = _Start + this->count();
        }
        else
        {
            _Start_u = _Start;
        }

        if (_End < 0)
        {
            _End_u = _End + this->count();
        }
        else
        {
            _End_u = _End;
        }

        vector<_Ty> _Subset;

        for (_NSTD size_t i = _Start_u; i <= _End_u; i++)
        {
            _Subset.append(this->_MY_VEC[i]);
        }

        return _Subset;
    }

    void clear()
    {
        this->_MY_VEC.clear();
    }
};

#endif // NSTD_USE_STD_OVERLAY

#endif // _NSVECTOR_

/*
 * Copyright (c) by R. Wilson. All rights reserved.
 * Consult your license regarding permissions and restrictions.
V2.1:0015 */

// #load "src/Belte/Standard/List.blt"
