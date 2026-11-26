/* @DSN-LIBRARY 1|logic-standard-v4|EX_BANK00,EX_BANK01,EX_BANK02,EX_BANK03,EX_BANK04,EX_BANK05,EX_BANK06,EX_BANK07,EX_BANK08,EX_BANK09,EX_BANK10,EX_BANK11,EX_BANK12,EX_ZERO,DIV4 */

/* @DSN-FUNCTION MBITW| */
/* @DSN-PORT X1|word|1|read|bit-word|none||false| */
/* @DSN-PORT X2|constant|1|constant|mask|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MBITW p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    li t1,\p1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    bne s8,zero,.LL1\@
    li t0,1
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LL1\@:
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MWBIT| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|constant|1|constant|mask|none||false| */
/* @DSN-PORT Y|word|1|readwrite|bit-word|word16||false| */
.macro MWBIT p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p2
    lhu t0,0(t6)
    mv s1,t0
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    bne s8,zero,.LL1\@
    mv t0,s1
    li t1,\p1
    slli t1,t1,16
    srli t1,t1,16
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    j .LL2\@
.LL1\@:
    li t0,\p1
    slli t0,t0,16
    srli t0,t0,16
    mv s3,t0
    mv t0,s3
    not t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    mv t0,s1
    mv t1,s5
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
.LL2\@:
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
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
    li t6,\p0
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    beq s8,zero,.LTIDEL\@
    li t6,\p1
    lhu t0,0(t6)
    mv s1,t0
    mv t0,s1
    li t6,.LVTIMS\@
    sh t0,0(t6)
    j .LETIDEL\@
.LTIDEL\@:
    li t6,.LVTIMS\@
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LETIDEL\@
    xor t6,s9,s10
    bne t6,zero,.LETIDEL\@
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    mv t0,s4
    li t6,.LVTIMS\@
    sh t0,0(t6)
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LETIDEL\@:
    mv t0,s4
    li t6,\p3
    sh t0,0(t6)
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
    li t6,\p0
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LTIDEL\@
    li t6,\p1
    lhu t0,0(t6)
    mv s1,t0
    mv t0,s1
    li t6,.LVTIMV\@
    sh t0,0(t6)
    j .LETIDEL\@
.LTIDEL\@:
    li t6,.LVTIMV\@
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LETIDEL\@
    xor t6,s9,s10
    bne t6,zero,.LETIDEL\@
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    mv t0,s4
    li t6,.LVTIMV\@
    sh t0,0(t6)
    li t0,1
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LETIDEL\@:
    mv t0,s4
    li t6,\p3
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MNO| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MNO p0,p1
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    not t2,t0
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p1
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MOR2N| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR2N p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s0
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    not t2,t0
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MOR3| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR3 p0,p1,p2,p3
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s0
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p2
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p3
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION M2NAND3| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro M2NAND3 p0,p1,p2,p3
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    not t2,t0
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s2
    not t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    mv t0,s4
    mv t1,s5
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p2
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p3
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MALARM| */
/* @DSN-PORT X1|word|1|read|bit-word|none||false| */
/* @DSN-PORT X2|constant|1|constant|mask|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MALARM p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t0,\p1
    slli t0,t0,16
    srli t0,t0,16
    mv s2,t0
    mv t0,s0
    mv t1,s2
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    bne s8,zero,.LL1\@
    li t0,1
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LL1\@:
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MAND2| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MAND2 p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s0
    mv t1,s2
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MAND2N| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MAND2N p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s0
    mv t1,s2
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    not t2,t0
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MAND3| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MAND3 p0,p1,p2,p3
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    li t6,\p2
    lhu t0,0(t6)
    mv s3,t0
    mv t0,s0
    mv t1,s2
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    mv t1,s3
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p3
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MAND4| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT X4|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MAND4 p0,p1,p2,p3,p4
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    li t6,\p2
    lhu t0,0(t6)
    mv s3,t0
    mv t0,s0
    mv t1,s2
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    mv t1,s3
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p3
    lhu t0,0(t6)
    mv s3,t0
    mv t0,s4
    mv t1,s3
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p4
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MKEY21| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MKEY21 p0,p1,p2,p3
    li t6,\p2
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    beq s8,zero,.LMEN5\@
    li t6,\p0
    lhu t0,0(t6)
    mv s4,t0
    j .LLAB5\@
.LMEN5\@:
    li t6,\p1
    lhu t0,0(t6)
    mv s4,t0
.LLAB5\@:
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p3
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MKEY_2| */
/* @DSN-PORT X1|word|1|read|data|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y1|word|1|write|data|conditional-boolean|X1|false| */
/* @DSN-PORT Y2|word|1|write|data|conditional-boolean|X1|false| */
.macro MKEY_2 p0,p1,p2,p3
    li t6,\p1
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p0
    lhu t0,0(t6)
    mv s2,t0
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s3,t0
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    beq s8,zero,.LLAB2\@
    mv t0,s2
    li t6,\p2
    sh t0,0(t6)
    mv t0,s3
    li t6,\p3
    sh t0,0(t6)
    j .LMENT\@
.LLAB2\@:
    mv t0,s2
    li t6,\p3
    sh t0,0(t6)
    mv t0,s3
    li t6,\p2
    sh t0,0(t6)
.LMENT\@:
    addi zero,zero,0
.endm

/* @DSN-FUNCTION MNAND2| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MNAND2 p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    not t2,t0
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MNAND3| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MNAND3 p0,p1,p2,p3
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    not t2,t0
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p2
    lhu t0,0(t6)
    mv s3,t0
    mv t0,s4
    mv t1,s3
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p3
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MOR2| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR2 p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s0
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MOR4| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|word|1|read|logical|none||false| */
/* @DSN-PORT X3|word|1|read|logical|none||false| */
/* @DSN-PORT X4|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MOR4 p0,p1,p2,p3,p4
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s0
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p2
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p3
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p4
    sh t0,0(t6)
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
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s0
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p2
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p3
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p4
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p5
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p6
    sh t0,0(t6)
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
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s0
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p2
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p3
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p4
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p5
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p6
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p7
    sh t0,0(t6)
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
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s0
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p2
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p3
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p4
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p5
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p6
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p7
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    li t6,\p8
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s4
    mv t1,s2
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p9
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MRESET| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|2|readwrite|bit-word|word16||false| */
.macro MRESET p0,p1
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    bne s8,zero,.LL1\@
    li t6,\p1
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    li t1,32768
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p1
    sh t0,0(t6)
    li t6,\p1+2
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    li t1,32768
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p1+2
    sh t0,0(t6)
.LL1\@:
    addi zero,zero,0
.endm

/* @DSN-FUNCTION MSET| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT Y|word|2|readwrite|bit-word|word16||false| */
.macro MSET p0,p1
    .pushsection .data
    .LVSET\@:
    .short 0
    .popsection
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    bne s8,zero,.LL1\@
    li t6,\p1
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    li t1,0X7FFF
    slli t1,t1,16
    srli t1,t1,16
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p1
    sh t0,0(t6)
    li t6,\p1+2
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    li t1,0X7FFF
    slli t1,t1,16
    srli t1,t1,16
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p1+2
    sh t0,0(t6)
    li t0,1
    slli t0,t0,16
    srli t0,t0,16
    mv s0,t0
    mv t0,s0
    li t6,.LVSET\@
    sh t0,0(t6)
    j .LSETFF\@
.LL1\@:
    li t6,.LVSET\@
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    mv t2,t0
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    bne s8,zero,.LSETFF\@
    li t6,\p1
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    li t1,0X8000
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p1
    sh t0,0(t6)
    li t6,\p1+2
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    li t1,0X8000
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p1+2
    sh t0,0(t6)
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s0,t0
    mv t0,s0
    li t6,.LVSET\@
    sh t0,0(t6)
.LSETFF\@:
    addi zero,zero,0
.endm

/* @DSN-FUNCTION MTGAL| */
/* @DSN-PORT X1|word|1|read|logical|none||false| */
/* @DSN-PORT X2|constant|1|constant|mask|none||false| */
/* @DSN-PORT Y|word|1|readwrite|bit-word|word16||false| */
.macro MTGAL p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    bne s8,zero,.LL1\@
    li t6,\p2
    lhu t0,0(t6)
    mv s1,t0
    mv t0,s1
    li t1,\p1
    slli t1,t1,16
    srli t1,t1,16
    or t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.LL1\@:
    addi zero,zero,0
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
    li t6,\p0
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    beq s8,zero,.LTIDEL\@
    li t0,(\p1)
    slli t0,t0,16
    srli t0,t0,16
    mv s1,t0
    mv t0,s1
    li t6,\p2
    sh t0,0(t6)
    j .LETIDEL\@
.LTIDEL\@:
    li t6,\p2
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LETIDEL\@
    xor t6,s9,s10
    bne t6,zero,.LETIDEL\@
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LETIDEL\@:
    mv t0,s4
    li t6,\p3
    sh t0,0(t6)
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
    li t6,\p0
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    beq s8,zero,.LTIDEL\@
    li t6,\p1
    lhu t0,0(t6)
    mv s1,t0
    mv t0,s1
    li t6,.LVTIMS\@
    sh t0,0(t6)
    j .LETIDEL\@
.LTIDEL\@:
    li t6,.LVTIMS\@
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LETIDEL\@
    xor t6,s9,s10
    bne t6,zero,.LETIDEL\@
    mv t0,s0
    mv s4,t0
    li t6,DIV4
    lhu t0,0(t6)
    mv s2,t0
    mv t0,s2
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LGENEXEC1\@
    j .LGENEND1\@
.LGENEXEC1\@:
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
.LGENEND1\@:
    mv t0,s4
    li t6,.LVTIMS\@
    sh t0,0(t6)
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LETIDEL\@:
    mv t0,s4
    li t6,\p3
    sh t0,0(t6)
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
    li t6,\p0
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    beq s8,zero,.LACTIVE\@
    li t0,\p1
    slli t0,t0,16
    srli t0,t0,16
    mv s1,t0
    mv t0,s1
    li t6,.LCOUNTER\@
    sh t0,0(t6)
    j .LEND\@
.LACTIVE\@:
    li t6,.LCOUNTER\@
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LEND\@
    xor t6,s9,s10
    bne t6,zero,.LEND\@
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    mv t0,s4
    li t6,.LCOUNTER\@
    sh t0,0(t6)
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LEND\@:
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
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
    li t6,\p0
    lhu t0,0(t6)
    mv s4,t0
    mv t0,s4
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LRELEASE\@
    li t0,\p1
    slli t0,t0,16
    srli t0,t0,16
    mv s1,t0
    mv t0,s1
    li t6,.LCOUNTER\@
    sh t0,0(t6)
    j .LEND\@
.LRELEASE\@:
    li t6,.LCOUNTER\@
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LEND\@
    xor t6,s9,s10
    bne t6,zero,.LEND\@
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    mv t0,s4
    li t6,.LCOUNTER\@
    sh t0,0(t6)
    li t0,1
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LEND\@:
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.endm

/* @DSN-FUNCTION MKEY_3| */
/* @DSN-PORT X1|word|1|read|data|none||false| */
/* @DSN-PORT X2|word|1|read|selection|none||false| */
/* @DSN-PORT Y1|word|1|write|data|conditional-boolean|X1|false| */
/* @DSN-PORT Y2|word|1|write|data|conditional-boolean|X1|false| */
/* @DSN-PORT Y3|word|1|write|data|conditional-boolean|X1|false| */
.macro MKEY_3 p0,p1,p2,p3,p4
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t6,\p1
    lhu t0,0(t6)
    mv s1,t0
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s2,t0
    mv t0,s2
    li t6,\p2
    sh t0,0(t6)
    mv t0,s2
    li t6,\p3
    sh t0,0(t6)
    mv t0,s2
    li t6,\p4
    sh t0,0(t6)
    mv t0,s1
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LFIRST\@
    mv t0,s1
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    xor t3,t0,t1
    xor t4,t0,s5
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    bne s8,zero,.LSECOND\@
    li t0,2
    slli t0,t0,16
    srli t0,t0,16
    mv s3,t0
    mv t0,s1
    mv t1,s3
    sub t2,t0,t1
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    xor t3,t0,t1
    xor t4,t0,s5
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    bne s8,zero,.LTHIRD\@
    j .LEND\@
.LFIRST\@:
    mv t0,s0
    li t6,\p2
    sh t0,0(t6)
    j .LEND\@
.LSECOND\@:
    mv t0,s0
    li t6,\p3
    sh t0,0(t6)
    j .LEND\@
.LTHIRD\@:
    mv t0,s0
    li t6,\p4
    sh t0,0(t6)
.LEND\@:
    addi zero,zero,0
.endm

/* @DSN-FUNCTION MUPRKD|EX_BANK00,EX_BANK01,EX_BANK02,EX_BANK03,EX_BANK04,EX_BANK05,EX_BANK06,EX_BANK07,EX_BANK08,EX_BANK09,EX_BANK10,EX_BANK11,EX_BANK12 */
/* @DSN-PORT X1|word|1|read|selection|none||false| */
/* @DSN-PORT X2|constant|1|constant|selection-default|none||false| */
/* @DSN-PORT Y|word|1|write|logical|boolean||false| */
.macro MUPRKD p0,p1,p2
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    li t0,0XFFFF
    slli t0,t0,16
    srli t0,t0,16
    mv s2,t0
    mv t0,s0
    mv t1,s2
    sub t2,t0,t1
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    xor t3,t0,t1
    xor t4,t0,s5
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    beq s8,zero,.LCHOSEN\@
    li t0,\p1
    slli t0,t0,16
    srli t0,t0,16
    mv s0,t0
.LCHOSEN\@:
    mv t0,s0
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LZERO\@
    xor t6,s9,s10
    bne t6,zero,.LZERO\@
    li t0,208
    slli t0,t0,16
    srli t0,t0,16
    mv s2,t0
    mv t0,s0
    mv t1,s2
    sub t2,t0,t1
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    xor t3,t0,t1
    xor t4,t0,s5
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    bne s8,zero,.LGEN2\@
    xor t6,s9,s10
    beq t6,zero,.LZERO\@
.LGEN2\@:
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s1,t0
    li t0,16
    slli t0,t0,16
    srli t0,t0,16
    mv s2,t0
.LWORD_LOOP\@:
    mv t0,s4
    mv t1,s2
    sub t2,t0,t1
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    xor t3,t0,t1
    xor t4,t0,s5
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    xor t6,s9,s10
    bne t6,zero,.LWORD_FOUND\@
    mv t0,s5
    mv s4,t0
    mv t0,s1
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    add t2,t0,t1
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    xor t3,t0,t1
    not t3,t3
    xor t4,t0,s5
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    srli s11,t2,16
    andi s11,s11,1
    mv t0,s5
    mv s1,t0
    j .LWORD_LOOP\@
.LWORD_FOUND\@:
    mv t0,s4
    mv s0,t0
    li t0,(EX_BANK00/2)
    slli t0,t0,16
    srli t0,t0,16
    mv s2,t0
    mv t0,s1
    mv t1,s2
    add t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    not t3,t3
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    srli s11,t2,16
    andi s11,s11,1
    mv t0,s4
    mv s6,t0
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s7,t0
    slli t6,s6,1
    lhu t0,0(t6)
    mv s1,t0
    li t0,1
    slli t0,t0,16
    srli t0,t0,16
    mv s2,t0
    mv t0,s0
    mv t2,t0
    slli s5,t2,16
    srli s5,s5,16
    sltiu s8,s5,1
    srli s9,s5,15
    li s10,0
    li s11,0
    bne s8,zero,.LBIT_READ\@
.LBIT_LOOP\@:
    mv t0,s2
    mv t1,s2
    add t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    not t3,t3
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    srli s11,t2,16
    andi s11,s11,1
    mv t0,s4
    mv s2,t0
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    mv t0,s4
    mv s0,t0
    beq s8,zero,.LBIT_LOOP\@
.LBIT_READ\@:
    mv t0,s1
    mv t1,s2
    and t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    li s10,0
    li s11,0
    bne s8,zero,.LSTORE\@
    li t0,1
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
    j .LSTORE\@
.LZERO\@:
    li t0,0
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LSTORE\@:
    mv t0,s4
    li t6,\p2
    sh t0,0(t6)
.LEND\@:
    addi zero,zero,0
.endm

.macro EX_COPY p0,p1
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    li t6,\p1
    sh t0,0(t6)
.endm

.macro EX_DIV8 p0
    li t6,\p0
    lhu t0,0(t6)
    mv s0,t0
    mv t0,s0
    li t1,1
    slli t1,t1,16
    srli t1,t1,16
    sub t2,t0,t1
    slli s4,t2,16
    srli s4,s4,16
    sltiu s8,s4,1
    srli s9,s4,15
    xor t3,t0,t1
    xor t4,t0,s4
    and t3,t3,t4
    srli s10,t3,15
    andi s10,s10,1
    sltu s11,t0,t1
    xori s11,s11,1
    xor t6,s9,s10
    beq t6,zero,.LEND\@
    li t0,7
    slli t0,t0,16
    srli t0,t0,16
    mv s4,t0
.LEND\@:
    mv t0,s4
    li t6,\p0
    sh t0,0(t6)
.endm
