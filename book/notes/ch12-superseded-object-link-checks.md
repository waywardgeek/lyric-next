# Superseded Chapter 12 object-link rejection experiments

These were provisional author-added expectations, not accepted Chapter 12 safety
requirements. Bill clarified that ordinary link writes are temporarily allowed;
complete safety awaits controlled relation mutation and generated detachment.
Do not run these programs: some deliberately access destroyed storage. Do not
restore all expectations automatically in the relation chapter: managed teardown
changes the behavior, including clearing links during destruction. Independently
saved local references still need ordinary lifetime protection.

Preserved verbatim from the pending grader for evidence, not as executable tests:

```python
    # Ordinary stored handles do not retain the referent. Borrow checking must
    # reject destruction followed by use, including through aliases and calls.
    ('object-field-attached-link-blocks-destruction', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func main() {
        let box=Box(7); let h=Holder(box)
        box.destroy()
    }
    ''', 'LYC330'),
    ('object-field-destroy-before-detach-in-call', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func dispose(h:Holder, box:Box) { box.destroy(); h.item=null }
    func main() {
        let box=Box(7); let h=Holder(box)
        dispose(h,box); h.destroy()
    }
    ''', 'LYC330'),
    ('object-field-saved-borrow-after-detach', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func main() {
        let box=Box(7); let h=Holder(box); let saved=h.item!
        h.item=null; box.destroy(); assert(saved.n==7)
    }
    ''', 'LYC330'),
    ('object-field-other-holder-still-attached', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func main() {
        let box=Box(7); let first=Holder(box); let second=Holder(box)
        first.item=null; box.destroy(); assert(second.item!.n==7)
    }
    ''', 'LYC330'),
    ('object-field-call-does-not-clear-other-holder', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func detach(h:Holder) { h.item=null }
    func main() {
        let box=Box(7); let first=Holder(box); let second=Holder(box)
        detach(first); box.destroy(); assert(second.item!.n==7)
    }
    ''', 'LYC330'),
    ('object-field-returned-alias-preserves-other-holder', BOX + '''class Holder {
        item:Box?
        func Holder(self,item:Box) { self.item=item }
    }
    func identity(h:Holder)->Holder { return h }
    func main() {
        let box=Box(7); let first=Holder(box); let second=Holder(box)
        let alias=identity(first); alias.item=null
        box.destroy(); assert(second.item!.n==7)
    }
    ''', 'LYC330'),
    ('object-field-destroyed-referent', BOX + '''class Holder {
        item:Box
        func Holder(self,item:Box) { self.item=item }
    }
    func main() { let box=Box(1); let h=Holder(box); box.destroy(); let n=h.item.n }
    ''', 'LYC330'),
```
