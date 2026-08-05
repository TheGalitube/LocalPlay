package io.localplay.receiver

import android.Manifest
import android.app.Activity
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.view.SurfaceHolder
import android.view.SurfaceView
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import io.localplay.receiver.data.ReceiverPreferences
import io.localplay.receiver.engine.NativeReceiverBridge
import io.localplay.receiver.model.ReceiverConfig
import io.localplay.receiver.model.ReceiverPhase
import io.localplay.receiver.service.ReceiverService
import io.localplay.receiver.state.ReceiverStateStore

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            LocalPlayTheme {
                LocalPlayApp()
            }
        }
    }
}

@Composable
private fun LocalPlayApp() {
    val context = LocalContext.current
    val activity = context as? Activity
    val status by ReceiverStateStore.status.collectAsStateWithLifecycle()
    val connected = status.phase == ReceiverPhase.CONNECTED

    DisposableEffect(activity, connected) {
        activity?.setPlaybackFullscreen(connected)
        onDispose {
            if (connected) activity?.setPlaybackFullscreen(false)
        }
    }

    if (connected) {
        ReceiverSurface()
    } else {
        DashboardScreen()
    }
}

private fun Activity.setPlaybackFullscreen(enabled: Boolean) {
    val insetsController = WindowCompat.getInsetsController(window, window.decorView)
    if (enabled) {
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        insetsController.systemBarsBehavior =
            WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        insetsController.hide(WindowInsetsCompat.Type.systemBars())
    } else {
        window.clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        insetsController.show(WindowInsetsCompat.Type.systemBars())
    }
}

@Composable
private fun DashboardScreen() {
    val context = LocalContext.current
    val preferences = remember { ReceiverPreferences(context) }
    val initialConfig = remember { preferences.load() }
    val status by ReceiverStateStore.status.collectAsStateWithLifecycle()
    val engineReady = remember { NativeReceiverBridge.isEngineReady() }

    var receiverName by remember { mutableStateOf(initialConfig.name) }
    var validationError by remember { mutableStateOf<String?>(null) }

    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions(),
    ) { }

    LaunchedEffect(Unit) {
        val permissions = buildList {
            if (Build.VERSION.SDK_INT >= 33 &&
                ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) !=
                PackageManager.PERMISSION_GRANTED
            ) {
                add(Manifest.permission.POST_NOTIFICATIONS)
            }
            if (Build.VERSION.SDK_INT >= 37 &&
                ContextCompat.checkSelfPermission(context, LOCAL_NETWORK_PERMISSION) !=
                PackageManager.PERMISSION_GRANTED
            ) {
                add(LOCAL_NETWORK_PERMISSION)
            }
        }
        if (permissions.isNotEmpty()) permissionLauncher.launch(permissions.toTypedArray())
    }

    Scaffold(
        contentWindowInsets = WindowInsets.safeDrawing,
        topBar = { AppHeader() },
    ) { contentPadding ->
        BoxWithConstraints(
            modifier = Modifier
                .fillMaxSize()
                .padding(contentPadding),
            contentAlignment = Alignment.TopCenter,
        ) {
            val wide = maxWidth >= 760.dp
            val horizontalPadding = if (maxWidth < 420.dp) 16.dp else 24.dp
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .imePadding()
                    .padding(
                        start = horizontalPadding,
                        top = 20.dp,
                        end = horizontalPadding,
                        bottom = 32.dp,
                    ),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                if (wide) {
                    Row(
                        modifier = Modifier
                            .widthIn(max = 1080.dp)
                            .fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(20.dp),
                    ) {
                        Column(
                            modifier = Modifier.weight(1.1f),
                            verticalArrangement = Arrangement.spacedBy(16.dp),
                        ) {
                            HeroCard(status.phase, status.message)
                            HowToCard()
                        }
                        SettingsCard(
                            modifier = Modifier.weight(0.9f),
                            receiverName = receiverName,
                            validationError = validationError,
                            running = status.isRunning,
                            engineReady = engineReady,
                            onNameChange = {
                                receiverName = it.take(40)
                                validationError = null
                            },
                            onStart = {
                                validationError = startReceiver(context, preferences, receiverName)
                            },
                            onStop = {
                                context.startService(ReceiverService.stopIntent(context))
                            },
                        )
                    }
                } else {
                    Column(
                        modifier = Modifier
                            .widthIn(max = 620.dp)
                            .fillMaxWidth(),
                        verticalArrangement = Arrangement.spacedBy(16.dp),
                    ) {
                        HeroCard(status.phase, status.message)
                        SettingsCard(
                            receiverName = receiverName,
                            validationError = validationError,
                            running = status.isRunning,
                            engineReady = engineReady,
                            onNameChange = {
                                receiverName = it.take(40)
                                validationError = null
                            },
                            onStart = {
                                validationError = startReceiver(context, preferences, receiverName)
                            },
                            onStop = {
                                context.startService(ReceiverService.stopIntent(context))
                            },
                        )
                        HowToCard()
                    }
                }
            }
        }
    }
}

@Composable
private fun AppHeader() {
    Surface(tonalElevation = 2.dp) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 24.dp, vertical = 18.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            Surface(
                modifier = Modifier.size(44.dp),
                shape = RoundedCornerShape(14.dp),
                color = MaterialTheme.colorScheme.primaryContainer,
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text("LP", style = MaterialTheme.typography.titleMedium)
                }
            }
            Column {
                Text("LocalPlay", style = MaterialTheme.typography.titleLarge)
                Text(
                    "AirPlay-Empfänger für Android",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
private fun HeroCard(phase: ReceiverPhase, message: String) {
    val accent = when (phase) {
        ReceiverPhase.STOPPED -> MaterialTheme.colorScheme.outline
        ReceiverPhase.STARTING -> MaterialTheme.colorScheme.tertiary
        ReceiverPhase.ADVERTISING -> Color(0xFF1B8D58)
        ReceiverPhase.CONNECTED -> MaterialTheme.colorScheme.primary
        ReceiverPhase.ERROR -> MaterialTheme.colorScheme.error
    }
    ElevatedCard(
        colors = CardDefaults.elevatedCardColors(
            containerColor = MaterialTheme.colorScheme.surfaceContainerLow,
        ),
        shape = RoundedCornerShape(28.dp),
    ) {
        Column(
            modifier = Modifier.padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(10.dp),
            ) {
                Box(Modifier.size(10.dp).background(accent, CircleShape))
                Text(phase.label(), style = MaterialTheme.typography.labelLarge, color = accent)
            }
            Text(
                if (phase == ReceiverPhase.ADVERTISING) "Bereit zum Spiegeln" else "Android als Bildschirm",
                style = MaterialTheme.typography.headlineMedium,
            )
            Text(
                message,
                style = MaterialTheme.typography.bodyLarge,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Text(
                "Das Videobild erscheint automatisch und ohne Bedienelemente im Vollbild, " +
                    "sobald ein AirPlay-Gerät verbunden ist.",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun SettingsCard(
    receiverName: String,
    validationError: String?,
    running: Boolean,
    engineReady: Boolean,
    onNameChange: (String) -> Unit,
    onStart: () -> Unit,
    onStop: () -> Unit,
    modifier: Modifier = Modifier,
) {
    ElevatedCard(modifier = modifier, shape = RoundedCornerShape(28.dp)) {
        Column(
            modifier = Modifier.padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Text("Empfänger", style = MaterialTheme.typography.titleLarge)
            OutlinedTextField(
                value = receiverName,
                onValueChange = onNameChange,
                modifier = Modifier.fillMaxWidth(),
                enabled = !running,
                label = { Text("AirPlay-Name") },
                singleLine = true,
                isError = validationError != null,
                supportingText = {
                    Text(validationError ?: "So erscheint das Gerät auf iPhone, iPad und Mac.")
                },
            )
            if (!engineReady) {
                Text(
                    "Der native Empfängerkern ist in diesem Build nicht verfügbar.",
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodyMedium,
                )
            }
            if (running) {
                OutlinedButton(modifier = Modifier.fillMaxWidth(), onClick = onStop) {
                    Text("Empfänger beenden")
                }
            } else {
                Button(
                    modifier = Modifier.fillMaxWidth(),
                    enabled = engineReady,
                    onClick = onStart,
                ) {
                    Text("Empfänger starten")
                }
            }
            Text(
                "Lokales Netzwerk · kein Konto · kein Cloud-Upload",
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                style = MaterialTheme.typography.bodySmall,
            )
        }
    }
}

@Composable
private fun HowToCard() {
    ElevatedCard(shape = RoundedCornerShape(28.dp)) {
        Column(
            modifier = Modifier.padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Text("Verbinden", style = MaterialTheme.typography.titleLarge)
            Text("1  Empfänger starten", style = MaterialTheme.typography.bodyLarge)
            Text("2  Auf dem Apple-Gerät Bildschirmspiegelung öffnen", style = MaterialTheme.typography.bodyLarge)
            Text("3  Den angezeigten LocalPlay-Namen auswählen", style = MaterialTheme.typography.bodyLarge)
        }
    }
}

private fun startReceiver(
    context: android.content.Context,
    preferences: ReceiverPreferences,
    receiverName: String,
): String? = try {
    val config = ReceiverConfig(name = receiverName, requirePin = false).validate()
    preferences.save(config)
    ContextCompat.startForegroundService(context, ReceiverService.startIntent(context, config))
    null
} catch (error: IllegalArgumentException) {
    error.message
}

@Composable
private fun ReceiverSurface() {
    val status by ReceiverStateStore.status.collectAsStateWithLifecycle()
    val playbackReady by NativeReceiverBridge.playbackReady.collectAsStateWithLifecycle()

    Box(
        modifier = Modifier.fillMaxSize().background(Color.Black),
        contentAlignment = Alignment.Center,
    ) {
        AndroidView(
            modifier = Modifier.fillMaxSize(),
            factory = { context ->
                SurfaceView(context).apply {
                    setBackgroundColor(android.graphics.Color.BLACK)
                    holder.addCallback(object : SurfaceHolder.Callback {
                        override fun surfaceCreated(holder: SurfaceHolder) {
                            NativeReceiverBridge.setSurface(holder.surface)
                        }

                        override fun surfaceChanged(
                            holder: SurfaceHolder,
                            format: Int,
                            width: Int,
                            height: Int,
                        ) {
                            NativeReceiverBridge.setSurface(holder.surface)
                        }

                        override fun surfaceDestroyed(holder: SurfaceHolder) {
                            NativeReceiverBridge.setSurface(null)
                        }
                    })
                }
            },
        )
        if (!playbackReady) {
            Column(
                horizontalAlignment = Alignment.CenterHorizontally,
                verticalArrangement = Arrangement.spacedBy(16.dp),
            ) {
                CircularProgressIndicator(color = Color.White)
                Text(
                    text = if (status.message.startsWith("Video konnte")) {
                        status.message
                    } else {
                        "Videostream wird vorbereitet …"
                    },
                    color = Color.White,
                    style = MaterialTheme.typography.bodyLarge,
                )
            }
        }
    }

    DisposableEffect(Unit) {
        onDispose { NativeReceiverBridge.setSurface(null) }
    }
}

private fun ReceiverPhase.label(): String = when (this) {
    ReceiverPhase.STOPPED -> "AUS"
    ReceiverPhase.STARTING -> "STARTET"
    ReceiverPhase.ADVERTISING -> "BEREIT"
    ReceiverPhase.CONNECTED -> "VERBUNDEN"
    ReceiverPhase.ERROR -> "FEHLER"
}

@Composable
private fun LocalPlayTheme(content: @Composable () -> Unit) {
    val context = LocalContext.current
    val dark = isSystemInDarkTheme()
    val colors = when {
        Build.VERSION.SDK_INT >= Build.VERSION_CODES.S && dark -> dynamicDarkColorScheme(context)
        Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> dynamicLightColorScheme(context)
        dark -> darkColorScheme(
            primary = Color(0xFFADC6FF),
            secondary = Color(0xFFB9C6EA),
            tertiary = Color(0xFFD9BDE4),
        )
        else -> lightColorScheme(
            primary = Color(0xFF2E5DA8),
            secondary = Color(0xFF535F70),
            tertiary = Color(0xFF705574),
        )
    }
    MaterialTheme(colorScheme = colors, content = content)
}

private const val LOCAL_NETWORK_PERMISSION = "android.permission.ACCESS_LOCAL_NETWORK"
