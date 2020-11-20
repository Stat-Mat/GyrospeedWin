; This routine is called after the headers have been loaded as it hijacks the BASIC idle loop vector at $0302.
; It then calls the loader code stored in the CBM header at offset $0351. It then finally starts
; the program after the turbo load has finished by performing a BASIC RUN.

*=$2bc

        ; Stores current border colour - not really needed and allows $fe to be used for the
        ; alternative loading bars/border flashing effect routines in the loader code
        ;lda $d020
        ;sta $fe
        
        ; Restore the BASIC idle loop vector (back to $a483)
        lda #$a4
        sta $0303
        lda #$83
        sta $0302
        
        ; Jump to the loader routine which is embedded in the CBM header
        ; and thus stored in the cassette buffer
        jsr $0351
        
        ; Restores previously stored border colour - removed as per comment above
        ;lda $fe
        ;sta $d020

        lda #$37
        sta $01 ; Stop the datasette motor

        ; This fix to the original code clears the keyboard buffer to prevent any unwanted keypresses
        ; from either a SHIFT RUN/STOP load or the user pressing space at the FOUND messsage
        ; This was causing issues with trainers etc

        lda #$00
        ldy #$0a ; the keyboard buffer at $277 has 10 bytes of storage
Loop    sta $0277,y 
        dey
        bpl Loop

        cli ; Enable IRQs
        lda #$1b
        sta $d011 ; Enable the screen
        jsr $ff84 ; Init interrupts

        ; Check the XOR checksum
        ; The original author seemed to have intentionally decided to skip this
        ; step by having the BNE instruction simply branch to the next instruction (jsr $a663).
        ; I have restored it as it really should show a load error to the user if the data
        ; hasn't been read correctly.
        lda $fc
        cmp $fb
        bne DisplayLoadError ; If the read and calculated checksums don't match, display load error

        jsr $a663 ; Do CLR without aborting I/O
        
        ; Check machine code ($00) or BASIC prog ($01)
        lda $03de
        beq JumpToBasicIdle
   
        jsr $a68e ; Set BASIC pointers to program start
        lda #$00
        sta $9d ; Suppress system messages
        jmp $a7ae ; Do BASIC RUN
        
JumpToBasicIdle
        jmp ($0302)

DisplayLoadError
        ldx #$1d ; LOAD error code
        jmp $a437 ; Call BASIC error message routine to display LOAD error message

        ; The following bytes must align with $300 (start of the BASIC vectors)

        ; This is just the default value for the warm reset vector
        BYTE $8b, $e3 ; $e38b

        ; Last two bytes replace the BASIC idle loop vector and cause a jump to the start
        ; of the code above once the headers have been read
        BYTE $bc, $02 ; $02bc
