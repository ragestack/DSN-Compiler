/* @DSN-LIBRARY 1|logic-standard-v4|EX_BANK00,EX_BANK01,EX_BANK02,EX_BANK03,EX_BANK04,EX_BANK05,EX_BANK06,EX_BANK07,EX_BANK08,EX_BANK09,EX_BANK10,EX_BANK11,EX_BANK12,EX_ZERO,DIV4 */

/* @DSN-FUNCTION MBITW| */
/* @DSN-PORT X1|word|1|read|bit-word|none||false| */
/* @DSN-PORT X2|constant|1|constant|mask|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MBITW p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w #(\p1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    jz .LL1\@
    mov.w #(1),r14
    mov.w r14,r8
.LL1\@:
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MWBIT| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|constant|1|constant|mask|none||false| */
/* @DSN-PORT Y|word|1|readwrite|bit-word|word16||false| */
.macro MWBIT p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p2),r14
    mov.w r14,r5
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    jz .LL1\@
    mov.w r5,r13
    mov.w #(\p1),r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    jmp .LL2\@
.LL1\@:
    mov.w #(\p1),r14
    mov.w r14,r7
    mov.w r7,r13
    mov.w r13,r9
    xor.w #-1,r9
    bic.w #257,r2
    mov.w r5,r13
    mov.w r9,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
.LL2\@:
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MTIMSR| */
/* @DSN-PORT X|word|1|read|logical|none||false| */
/* @DSN-PORT T|word|1|read|tick-setting|none||true|D */
/* @DSN-PORT D|constant|1|constant|initial-setting|none||false| */
/* @DSN-PORT Y|word|1|write|logical|conditional-boolean|X|false| */
.macro MTIMSR p0,p1,p2,p3
    .pushsection .data
    .LVTIMS\@:
    .short \p2
    .popsection
    .pushsection .dsn_init
    .long \p1
    .short \p2
    .popsection
    mov.w &(\p0),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jnz .LTIDEL\@
    mov.w &(\p1),r14
    mov.w r14,r5
    mov.w r5,r14
    mov.w r14,&(.LVTIMS\@)
    jmp .LETIDEL\@
.LTIDEL\@:
    mov.w &(.LVTIMS\@),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LETIDEL\@
    jl .LETIDEL\@
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    sub.w r14,r8
    mov.w r8,r14
    mov.w r14,&(.LVTIMS\@)
    mov.w #(0),r14
    mov.w r14,r8
.LETIDEL\@:
    mov.w r8,r14
    mov.w r14,&(\p3)
.endm

/* @DSN-FUNCTION MTIMVZ| */
/* @DSN-PORT X|word|1|read|logical|none||false| */
/* @DSN-PORT T|word|1|read|tick-setting|none||true|D */
/* @DSN-PORT D|constant|1|constant|initial-setting|none||false| */
/* @DSN-PORT Y|word|1|write|logical|conditional-boolean|X|false| */
.macro MTIMVZ p0,p1,p2,p3
    .pushsection .data
    .LVTIMV\@:
    .short 0
    .popsection
    .pushsection .dsn_init
    .long \p1
    .short \p2
    .popsection
    mov.w &(\p0),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LTIDEL\@
    mov.w &(\p1),r14
    mov.w r14,r5
    mov.w r5,r14
    mov.w r14,&(.LVTIMV\@)
    jmp .LETIDEL\@
.LTIDEL\@:
    mov.w &(.LVTIMV\@),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LETIDEL\@
    jl .LETIDEL\@
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    sub.w r14,r8
    mov.w r8,r14
    mov.w r14,&(.LVTIMV\@)
    mov.w #(1),r14
    mov.w r14,r8
.LETIDEL\@:
    mov.w r8,r14
    mov.w r14,&(\p3)
.endm

/* @DSN-FUNCTION MNO| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MNO p0,p1
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r8
    xor.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p1)
.endm

/* @DSN-FUNCTION MOR2N| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR2N p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w r13,r8
    xor.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MOR3| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR3 p0,p1,p2,p3
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p2),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p3)
.endm

/* @DSN-FUNCTION M2NAND3| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro M2NAND3 p0,p1,p2,p3
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r8
    xor.w #-1,r8
    bic.w #257,r2
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r6,r13
    mov.w r13,r9
    xor.w #-1,r9
    bic.w #257,r2
    mov.w r8,r13
    mov.w r9,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w &(\p2),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p3)
.endm

/* @DSN-FUNCTION MALARM| */
/* @DSN-PORT X1|word|1|read|bit-word|none||false| */
/* @DSN-PORT X2|constant|1|constant|mask|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MALARM p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w #(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    jz .LL1\@
    mov.w #(1),r14
    mov.w r14,r8
.LL1\@:
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MAND2| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MAND2 p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MAND2N| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MAND2N p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w r13,r8
    xor.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MAND3| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MAND3 p0,p1,p2,p3
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w &(\p2),r14
    mov.w r14,r7
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w r7,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p3)
.endm

/* @DSN-FUNCTION MAND4| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT X4|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MAND4 p0,p1,p2,p3,p4
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w &(\p2),r14
    mov.w r14,r7
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w r7,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w &(\p3),r14
    mov.w r14,r7
    mov.w r8,r13
    mov.w r7,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p4)
.endm

/* @DSN-FUNCTION MKEY21| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MKEY21 p0,p1,p2,p3
    mov.w &(\p2),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    jnz .LMEN5\@
    mov.w &(\p0),r14
    mov.w r14,r8
    jmp .LLAB5\@
.LMEN5\@:
    mov.w &(\p1),r14
    mov.w r14,r8
.LLAB5\@:
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p3)
.endm

/* @DSN-FUNCTION MKEY_2| */
/* @DSN-PORT X1|word|1|read|data|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y1|word|1|write|data|conditional-boolean|X1|false| */
/* @DSN-PORT Y2|word|1|write|data|conditional-boolean|X1|false| */
.macro MKEY_2 p0,p1,p2,p3
    mov.w &(\p1),r14
    mov.w r14,r4
    mov.w &(\p0),r14
    mov.w r14,r6
    mov.w #(0),r14
    mov.w r14,r7
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    jnz .LLAB2\@
    mov.w r6,r14
    mov.w r14,&(\p2)
    mov.w r7,r14
    mov.w r14,&(\p3)
    jmp .LMENT\@
.LLAB2\@:
    mov.w r6,r14
    mov.w r14,&(\p3)
    mov.w r7,r14
    mov.w r14,&(\p2)
.LMENT\@:
    nop
.endm

/* @DSN-FUNCTION MNAND2| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MNAND2 p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r8
    xor.w #-1,r8
    bic.w #257,r2
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MNAND3| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MNAND3 p0,p1,p2,p3
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r8
    xor.w #-1,r8
    bic.w #257,r2
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w &(\p2),r14
    mov.w r14,r7
    mov.w r8,r13
    mov.w r7,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p3)
.endm

/* @DSN-FUNCTION MOR2| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR2 p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MOR4| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT X4|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR4 p0,p1,p2,p3,p4
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p2),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p3),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p4)
.endm

/* @DSN-FUNCTION MOR6| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT X4|word|1|read|logical|none||false| */
/* @DSN-PORT X5|word|1|read|logical|none||false| */
/* @DSN-PORT X6|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR6 p0,p1,p2,p3,p4,p5,p6
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p2),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p3),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p4),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p5),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p6)
.endm

/* @DSN-FUNCTION MOR7| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT X4|word|1|read|logical|none||false| */
/* @DSN-PORT X5|word|1|read|logical|none||false| */
/* @DSN-PORT X6|word|1|read|logical|none||false| */
/* @DSN-PORT X7|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR7 p0,p1,p2,p3,p4,p5,p6,p7
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p2),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p3),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p4),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p5),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p6),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p7)
.endm

/* @DSN-FUNCTION MOR9| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT X4|word|1|read|logical|none||false| */
/* @DSN-PORT X5|word|1|read|logical|none||false| */
/* @DSN-PORT X6|word|1|read|logical|none||false| */
/* @DSN-PORT X7|word|1|read|logical|none||false| */
/* @DSN-PORT X8|word|1|read|logical|none||false| */
/* @DSN-PORT X9|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR9 p0,p1,p2,p3,p4,p5,p6,p7,p8,p9
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p2),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p3),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p4),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p5),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p6),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p7),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w &(\p8),r14
    mov.w r14,r6
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p9)
.endm

/* @DSN-FUNCTION MRESET| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|2|readwrite|bit-word|word16||false| */
.macro MRESET p0,p1
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    jz .LL1\@
    mov.w &(\p1),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w #(32768),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p1)
    mov.w &(\p1+2),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w #(32768),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p1+2)
.LL1\@:
    nop
.endm

/* @DSN-FUNCTION MSET| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|2|readwrite|bit-word|word16||false| */
.macro MSET p0,p1
    .pushsection .data
    .LVSET\@:
    .short 0
    .popsection
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    jz .LL1\@
    mov.w &(\p1),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w #(0X7FFF),r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p1)
    mov.w &(\p1+2),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w #(0X7FFF),r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p1+2)
    mov.w #(1),r14
    mov.w r14,r4
    mov.w r4,r14
    mov.w r14,&(.LVSET\@)
    jmp .LSETFF\@
.LL1\@:
    mov.w &(.LVSET\@),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w r13,r8
    bit.w #-1,r8
    bic.w #257,r2
    jz .LSETFF\@
    mov.w &(\p1),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w #(0X8000),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p1)
    mov.w &(\p1+2),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w #(0X8000),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p1+2)
    mov.w #(0),r14
    mov.w r14,r4
    mov.w r4,r14
    mov.w r14,&(.LVSET\@)
.LSETFF\@:
    nop
.endm

/* @DSN-FUNCTION MTGAL| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|constant|1|constant|mask|none||false| */
/* @DSN-PORT Y|word|1|readwrite|bit-word|word16||false| */
.macro MTGAL p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    jz .LL1\@
    mov.w &(\p2),r14
    mov.w r14,r5
    mov.w r5,r13
    mov.w #(\p1),r14
    mov.w r13,r8
    bis.w r14,r8
    bit.w #-1,r8
    bic.w #257,r2
    mov.w r8,r14
    mov.w r14,&(\p2)
.LL1\@:
    nop
.endm

/* @DSN-FUNCTION MTIMSC| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|constant|1|constant|tick-setting|none||false| */
/* @DSN-PORT X3|word|1|readwrite|counter|word16||true| */
/* @DSN-PORT Y|word|1|write|logical|conditional-boolean|X1|false| */
.macro MTIMSC p0,p1,p2,p3
    .pushsection .dsn_init
    .long \p2
    .short 1200
    .popsection
    mov.w &(\p0),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jnz .LTIDEL\@
    mov.w #((\p1)),r14
    mov.w r14,r5
    mov.w r5,r14
    mov.w r14,&(\p2)
    jmp .LETIDEL\@
.LTIDEL\@:
    mov.w &(\p2),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LETIDEL\@
    jl .LETIDEL\@
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    sub.w r14,r8
    mov.w r8,r14
    mov.w r14,&(\p2)
    mov.w #(0),r14
    mov.w r14,r8
.LETIDEL\@:
    mov.w r8,r14
    mov.w r14,&(\p3)
.endm

/* @DSN-FUNCTION MTIMSL|DIV4 */
/* @DSN-PORT X|word|1|read|logical|none||false| */
/* @DSN-PORT T|word|1|read|tick-setting|none||true|D */
/* @DSN-PORT D|constant|1|constant|initial-setting|none||false| */
/* @DSN-PORT Y|word|1|write|logical|conditional-boolean|X|false| */
.macro MTIMSL p0,p1,p2,p3
    .pushsection .data
    .LVTIMS\@:
    .short \p2
    .popsection
    .pushsection .dsn_init
    .long \p1
    .short \p2
    .popsection
    mov.w &(\p0),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jnz .LTIDEL\@
    mov.w &(\p1),r14
    mov.w r14,r5
    mov.w r5,r14
    mov.w r14,&(.LVTIMS\@)
    jmp .LETIDEL\@
.LTIDEL\@:
    mov.w &(.LVTIMS\@),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LETIDEL\@
    jl .LETIDEL\@
    mov.w r4,r14
    mov.w r14,r8
    mov.w &(DIV4),r14
    mov.w r14,r6
    mov.w r6,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LGENEXEC1\@
    jmp .LGENEND1\@
.LGENEXEC1\@:
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    sub.w r14,r8
.LGENEND1\@:
    mov.w r8,r14
    mov.w r14,&(.LVTIMS\@)
    mov.w #(0),r14
    mov.w r14,r8
.LETIDEL\@:
    mov.w r8,r14
    mov.w r14,&(\p3)
.endm

/* @DSN-FUNCTION MXB| */
/* @DSN-PORT X1|word|1|initialize|data|none||true|X2 */
/* @DSN-PORT X2|constant|1|constant|initializer|none||false| */
.macro MXB p0,p1
    .pushsection .dsn_init
    .long \p0
    .short \p1
    .popsection
.endm

/* @DSN-FUNCTION MTIMC| */
/* @DSN-PORT X|word|1|read|logical|none||false| */
/* @DSN-PORT T|constant|1|constant|tick-setting|none||false| */
/* @DSN-PORT Y|word|1|write|logical|conditional-boolean|X|false| */
.macro MTIMC p0,p1,p2
    .pushsection .data
    .LCOUNTER\@:
    .short \p1
    .popsection
    mov.w &(\p0),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jnz .LACTIVE\@
    mov.w #(\p1),r14
    mov.w r14,r5
    mov.w r5,r14
    mov.w r14,&(.LCOUNTER\@)
    jmp .LEND\@
.LACTIVE\@:
    mov.w &(.LCOUNTER\@),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LEND\@
    jl .LEND\@
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    sub.w r14,r8
    mov.w r8,r14
    mov.w r14,&(.LCOUNTER\@)
    mov.w #(0),r14
    mov.w r14,r8
.LEND\@:
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MTIVC| */
/* @DSN-PORT X|word|1|read|logical|none||false| */
/* @DSN-PORT T|constant|1|constant|tick-setting|none||false| */
/* @DSN-PORT Y|word|1|write|logical|conditional-boolean|X|false| */
.macro MTIVC p0,p1,p2
    .pushsection .data
    .LCOUNTER\@:
    .short 0
    .popsection
    mov.w &(\p0),r14
    mov.w r14,r8
    mov.w r8,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LRELEASE\@
    mov.w #(\p1),r14
    mov.w r14,r5
    mov.w r5,r14
    mov.w r14,&(.LCOUNTER\@)
    jmp .LEND\@
.LRELEASE\@:
    mov.w &(.LCOUNTER\@),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LEND\@
    jl .LEND\@
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    sub.w r14,r8
    mov.w r8,r14
    mov.w r14,&(.LCOUNTER\@)
    mov.w #(1),r14
    mov.w r14,r8
.LEND\@:
    mov.w r8,r14
    mov.w r14,&(\p2)
.endm

/* @DSN-FUNCTION MKEY_3| */
/* @DSN-PORT X1|word|1|read|data|none||false| */
/* @DSN-PORT X2|word|1|read|selection|none||false| */
/* @DSN-PORT Y1|word|1|write|data|conditional-boolean|X1|false| */
/* @DSN-PORT Y2|word|1|write|data|conditional-boolean|X1|false| */
/* @DSN-PORT Y3|word|1|write|data|conditional-boolean|X1|false| */
.macro MKEY_3 p0,p1,p2,p3,p4
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w &(\p1),r14
    mov.w r14,r5
    mov.w #(0),r14
    mov.w r14,r6
    mov.w r6,r14
    mov.w r14,&(\p2)
    mov.w r6,r14
    mov.w r14,&(\p3)
    mov.w r6,r14
    mov.w r14,&(\p4)
    mov.w r5,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LFIRST\@
    mov.w r5,r13
    mov.w #(1),r14
    mov.w r13,r9
    sub.w r14,r9
    jz .LSECOND\@
    mov.w #(2),r14
    mov.w r14,r7
    mov.w r5,r13
    mov.w r7,r14
    mov.w r13,r9
    sub.w r14,r9
    jz .LTHIRD\@
    jmp .LEND\@
.LFIRST\@:
    mov.w r4,r14
    mov.w r14,&(\p2)
    jmp .LEND\@
.LSECOND\@:
    mov.w r4,r14
    mov.w r14,&(\p3)
    jmp .LEND\@
.LTHIRD\@:
    mov.w r4,r14
    mov.w r14,&(\p4)
.LEND\@:
    nop
.endm

/* @DSN-FUNCTION MUPRKD|EX_BANK00,EX_BANK01,EX_BANK02,EX_BANK03,EX_BANK04,EX_BANK05,EX_BANK06,EX_BANK07,EX_BANK08,EX_BANK09,EX_BANK10,EX_BANK11,EX_BANK12 */
/* @DSN-PORT X1|word|1|read|selection|none||false| */
/* @DSN-PORT X2|constant|1|constant|selection-default|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MUPRKD p0,p1,p2
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w #(0XFFFF),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r9
    sub.w r14,r9
    jnz .LCHOSEN\@
    mov.w #(\p1),r14
    mov.w r14,r4
.LCHOSEN\@:
    mov.w r4,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LZERO\@
    jl .LZERO\@
    mov.w #(208),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r6,r14
    mov.w r13,r9
    sub.w r14,r9
    jz .LGEN2\@
    jge .LZERO\@
.LGEN2\@:
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    sub.w r14,r8
    mov.w #(0),r14
    mov.w r14,r5
    mov.w #(16),r14
    mov.w r14,r6
.LWORD_LOOP\@:
    mov.w r8,r13
    mov.w r6,r14
    mov.w r13,r9
    sub.w r14,r9
    jl .LWORD_FOUND\@
    mov.w r9,r14
    mov.w r14,r8
    mov.w r5,r13
    mov.w #(1),r14
    mov.w r13,r9
    add.w r14,r9
    mov.w r9,r14
    mov.w r14,r5
    jmp .LWORD_LOOP\@
.LWORD_FOUND\@:
    mov.w r8,r14
    mov.w r14,r4
    mov.w #((EX_BANK00/2)),r14
    mov.w r14,r6
    mov.w r5,r13
    mov.w r6,r14
    mov.w r13,r8
    add.w r14,r8
    mov.w r8,r14
    mov.w r14,r10
    mov.w #(0),r14
    mov.w r14,r11
    mov.w r2,r15
    mov.w r10,r13
    add.w r13,r13
    mov.w r15,r2
    mov.w 0(r13),r14
    mov.w r14,r5
    mov.w #(1),r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w r13,r9
    bit.w #-1,r9
    bic.w #257,r2
    jz .LBIT_READ\@
.LBIT_LOOP\@:
    mov.w r6,r13
    mov.w r6,r14
    mov.w r13,r8
    add.w r14,r8
    mov.w r8,r14
    mov.w r14,r6
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    sub.w r14,r8
    mov.w r8,r14
    mov.w r14,r4
    jnz .LBIT_LOOP\@
.LBIT_READ\@:
    mov.w r5,r13
    mov.w r6,r14
    mov.w r13,r8
    and.w r14,r8
    bic.w #257,r2
    jz .LSTORE\@
    mov.w #(1),r14
    mov.w r14,r8
    jmp .LSTORE\@
.LZERO\@:
    mov.w #(0),r14
    mov.w r14,r8
.LSTORE\@:
    mov.w r8,r14
    mov.w r14,&(\p2)
.LEND\@:
    nop
.endm

.macro EX_COPY p0,p1
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r14
    mov.w r14,&(\p1)
.endm

.macro EX_DIV8 p0
    mov.w &(\p0),r14
    mov.w r14,r4
    mov.w r4,r13
    mov.w #(1),r14
    mov.w r13,r8
    sub.w r14,r8
    jge .LEND\@
    mov.w #(7),r14
    mov.w r14,r8
.LEND\@:
    mov.w r8,r14
    mov.w r14,&(\p0)
.endm
