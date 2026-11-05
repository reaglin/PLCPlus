---
title: Boolean logic
---

# Boolean logic

TRUE = 1 = on. FALSE = 0 = off.

| A | B | A AND B | A OR B | A XOR B | NOT A |
|---|---|---|---|---|---|
| FALSE | FALSE | FALSE | FALSE | FALSE | TRUE |
| FALSE | TRUE | FALSE | TRUE | TRUE | TRUE |
| TRUE | FALSE | FALSE | TRUE | TRUE | FALSE |
| TRUE | TRUE | TRUE | TRUE | FALSE | FALSE |

| Operation | TRUE when | Textbook | In ladder |
|---|---|---|---|
| AND | every input is TRUE | A · B | contacts in series |
| OR | any input is TRUE | A + B | contacts in parallel |
| NOT | its input is FALSE | Ā or A′ | a normally closed contact `-|/|-` (XIO) |
| XOR | the two inputs differ | A ⊕ B | two branches in parallel: A with NOT B, and NOT A with B |

**Order of operations:**

1. Brackets
2. NOT (only the input or bracket just after it)
3. AND
4. XOR
5. OR

`A OR B AND C` means `A OR (B AND C)`. When in doubt, write the brackets.

**Rows:** n inputs make 2ⁿ rows (2, 4, 8, 16). List them in counting order, all FALSE first.

**Sentence to expression:** name each condition as a tag; "and" is AND, "or" is OR, "not" and "unless" are NOT (unless X = AND NOT X); bracket each idea that belongs together.
