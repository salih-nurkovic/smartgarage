import 'dart:io';

import 'package:flutter/services.dart';

/// Loads Roboto and the Material icons that ship with the Flutter SDK, so layout is measured
/// with the font the phone uses instead of the test font, whose every glyph is a full square.
///
/// The SDK is found from the test runner itself, which lives inside `bin/cache/artifacts`.
Future<void> loadAppFonts() async {
  var directory = File(Platform.resolvedExecutable).parent;
  while (directory.path != directory.parent.path && !directory.path.endsWith('artifacts')) {
    directory = directory.parent;
  }
  final fonts = '${directory.path}${Platform.pathSeparator}material_fonts${Platform.pathSeparator}';

  ByteData read(String name) => ByteData.sublistView(File('$fonts$name').readAsBytesSync());

  final roboto = FontLoader('Roboto');
  for (final weight in ['regular', 'medium', 'bold', 'black']) {
    roboto.addFont(Future.value(read('roboto-$weight.ttf')));
  }
  await roboto.load();
  await (FontLoader('MaterialIcons')..addFont(Future.value(read('materialicons-regular.otf')))).load();
}
