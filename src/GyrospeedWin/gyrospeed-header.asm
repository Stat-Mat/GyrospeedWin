*=$033c

        ; File type = $03 (PRG)
        BYTE $03

        ; Start address of Gyrospeed boot routine ($02bc)
        BYTE $bc, $02

        ; End address of Gyrospeed boot routine ($0304)
        BYTE $04, $03

        ; Blank space for the filename (16 bytes)
        BYTE $20, $20, $20, $20, $20, $20, $20, $20
        BYTE $20, $20, $20, $20, $20, $20, $20, $20

        jsr SetupAndWaitForTurboPilot

        ; Setup default values
        lda #$00
        sta $c1
        sta $fd
        sta $fe

        ; Read in and setup the pointer to the crunched program's start address
        jsr ReadNextByte
        tay
        jsr ReadNextByte
        sta $c2

        ; Read in and setup the pointer to beginning of the BASIC variable area (end of crunched program plus 1)
        jsr ReadNextByte
        sta $2d
        jsr ReadNextByte
        sta $2e

Loop1   jsr ReadNextByte
        sta ($c1),y ; Now that we have read a full byte, copy it to its correct location

        ; Update the XOR checksum
        eor $fc
        sta $fc

        iny
        bne NoInc
        inc $c2

        ; Check to see if we've reached the end of the crunched program data
NoInc   cpy $2d
        lda $c2
        sbc $2e
        bcc Loop1

        ; Read in the last byte which stores the XOR checksum of all the bytes and store it in $fb
        jsr ReadNextByte
        sta $fb

        ; Return back to the Gyrospeed boot routine
        rts

SetupAndWaitForTurboPilot
        lda #$07
        sta $01 ; Start the datasette motor
        lda #$0b
        sta $d011 ; Disable the screen

        ; Wait a little time for the motor to get up to speed
        ; Slightly iffy using whatever values happen to be in X and Y, but this is how it was...
Wait    dex
        bne Wait
        dey
        bne Wait

        sei ; disable IRQs
        sty $fc ; y is zero from loop above
        lda #$f8
        sty $dd05 ; Set the value
        sta $dd04 ; for timer A

        ; Wait for the turbo pilot/sync-sequence ($40 x $40 followed by a single $5a)
Loop2   jsr ReadNextBit
        rol $bd ; Move the bit read from the carry flag into bit 0 of $bd
        lda $bd
        cmp #$40
        bne Loop2 ; Wait until the last full byte read = $40
Loop3   jsr ReadNextByte
        cmp #$40
        beq Loop3 ; Keep reading full bytes whilst they are = $40
        cmp #$5a  ; Check to see if it's $5a, which marks the end of the pilot
        bne Loop2
        rts

ReadNextByte
        ; When this bit is rotated all the way to the carry flag, we know have read 8 bits
        lda #$01
        sta $bd

        ; Reads in 8 bits to make a byte
Loop4   jsr ReadNextBit
        rol $bd ; Rotate the bit read from the carry flag into bit 0 of $bd
        bcc Loop4

        ; Load the byte into the accumulator
        lda $bd
        rts

ReadNextBit

        ; Wait for bit 4 of $dc0d to be set (the cassette read line) which indicates a bit has been read
        lda #$10
WaitForBit
        bit $dc0d
        beq WaitForBit

        lda $dd0d ; Read the contents of the interrupt control and status register
        pha ; Push it onto the stack

        ; Reset timer A
        lda #$19
        sta $dd0e

        jsr UpdateLoadingEffect

        pla ; Retrieve the contents of the interrupt control and status register from the stack

        ; Shift the timer A overflow bit (bit 0 in $dd0d) into the carry flag
        ; If this bit is a 1 (timeout ocurred), the bit we read from the tape is a 1, otherwise we read a 0.
        lsr
        rts

; This is where an alternative loading bars/border flashing effect routine can be inserted
; But for now, we just have the original

; Loading effects can be upto 0x1c bytes in length
; They can freely use $fc, $fd or the X register (all of which are zeroed at start-up
; and remain persistent between calls) and the accumulator (but not Y)

; Entropy can come from:
; ($c1),y which points to last byte read
; $bd which stores the gradually increasing bits of the byte currently being read

; But remember, they must be quick to execute (i.e. no delays) as they are called every time a bit is read

UpdateLoadingEffect

        inc $d020
        rts

; Padding bytes to make the file 192 bytes in length (i.e. the same size as the cassette buffer)
        BYTE $20, $20, $20, $20, $20, $20, $20, $20
        BYTE $20, $20, $20, $20, $20, $20, $20, $20
        BYTE $20, $20, $20, $20, $20, $20, $20, $20
