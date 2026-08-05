package io.localplay.receiver.model

import org.junit.Assert.assertEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class ReceiverConfigTest {
    @Test
    fun validateTrimsReceiverName() {
        val config = ReceiverConfig(name = "  Wohnzimmer  ").validate()

        assertEquals("Wohnzimmer", config.name)
    }

    @Test
    fun validateRejectsEmptyName() {
        assertThrows(IllegalArgumentException::class.java) {
            ReceiverConfig(name = "   ").validate()
        }
    }

    @Test
    fun validateRejectsPortRangeThatCannotFitThreePorts() {
        assertThrows(IllegalArgumentException::class.java) {
            ReceiverConfig(portStart = 65534).validate()
        }
    }
}
